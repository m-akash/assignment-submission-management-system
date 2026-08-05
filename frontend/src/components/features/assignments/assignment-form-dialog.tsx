'use client';

import { useEffect, useRef, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Download, Loader2, Paperclip, Trash2, Upload } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  downloadAssignmentFile,
  useDeleteAssignmentFile,
  useSaveAssignment,
  useUploadAssignmentFile,
} from '@/hooks/use-assignments';
import { useMyTeacherMappings } from '@/hooks/use-admin-resources';
import { formatBytes } from '@/lib/format';
import { cn } from '@/lib/utils';
import { assignmentSchema, type AssignmentInput, type AssignmentValues } from '@/schemas';
import type { Assignment } from '@/types/api';

/** UX-only mirror of FileStorage:AllowedExtensions; the server re-checks the bytes. */
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];
const MAX_FILES = 5;

/** `datetime-local` needs "YYYY-MM-DDTHH:mm" in local time, not a UTC ISO string. */
function toLocalInput(iso: string): string {
  const date = new Date(iso);
  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

function defaultDeadline(): string {
  const week = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
  week.setMinutes(0, 0, 0);
  return toLocalInput(week.toISOString());
}

export function AssignmentFormDialog({
  open,
  onOpenChange,
  assignment,
  readOnly = false,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  assignment?: Assignment | null;
  /**
   * When true the dialog becomes a read-only view (used for admins): every
   * field is disabled, file actions are hidden, and the save button is gone.
   */
  readOnly?: boolean;
}) {
  const isEdit = !!assignment;
  const mappings = useMyTeacherMappings(open);
  const save = useSaveAssignment();
  const upload = useUploadAssignmentFile();
  const removeFile = useDeleteAssignmentFile();
  const fileInput = useRef<HTMLInputElement>(null);
  const files = assignment?.files ?? [];
  // Files picked before the assignment exists yet — uploaded right after creation
  // succeeds, so a teacher never has to reopen the dialog just to attach material.
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const attachmentCount = isEdit ? files.length : pendingFiles.length;

  // <what the fields hold, context, what validation produces> — `maxMarks` is coerced,
  // so the first and last are not the same type.
  const form = useForm<AssignmentInput, unknown, AssignmentValues>({
    resolver: zodResolver(assignmentSchema),
    defaultValues: {
      teachingMappingId: '',
      title: '',
      description: '',
      deadlineLocal: defaultDeadline(),
      maxMarks: 100,
      allowResubmission: true,
    },
  });

  /**
   * The mapping that corresponds to an existing assignment: same offering, same author.
   * Falls back to any mapping for the offering so the disabled picker still shows the right
   * class and course even if the admin has since reassigned who teaches it.
   */
  function mappingFor(existing: Assignment) {
    const options = mappings.data ?? [];
    return (
      options.find(
        (option) =>
          option.classCourseId === existing.classCourseId &&
          option.teacherId === existing.teacherId,
      ) ?? options.find((option) => option.classCourseId === existing.classCourseId)
    );
  }

  // Repopulate whenever the dialog opens so a reopened form never shows stale values.
  useEffect(() => {
    if (!open) return;
    setPendingFiles([]);
    form.reset(
      assignment
        ? {
            // Resolved from the assignment's offering and author rather than stored on it:
            // the assignment points at the offering directly, not at a mapping.
            teachingMappingId: mappingFor(assignment)?.id ?? '',
            title: assignment.title,
            description: assignment.description,
            deadlineLocal: toLocalInput(assignment.deadlineUtc),
            maxMarks: assignment.maxMarks,
            allowResubmission: assignment.allowResubmission,
          }
        : {
            teachingMappingId: '',
            title: '',
            description: '',
            deadlineLocal: defaultDeadline(),
            maxMarks: 100,
            allowResubmission: true,
          },
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, assignment, form, mappings.data]);

  async function onSubmit(values: AssignmentValues) {
    // Unpack the one choice the form makes into the two the API takes. teacherId only
    // matters for an admin creating on someone's behalf; the server ignores it for a
    // teacher and uses their token identity instead.
    const mapping = options.find((option) => option.id === values.teachingMappingId);

    const saved = await save.mutateAsync({
      id: assignment?.id,
      input: {
        classCourseId: mapping?.classCourseId ?? '',
        teacherId: mapping?.teacherId,
        title: values.title,
        description: values.description,
        // The API stores UTC; convert once, here.
        deadlineUtc: new Date(values.deadlineLocal).toISOString(),
        maxMarks: values.maxMarks,
        allowResubmission: values.allowResubmission,
      },
    });

    // The assignment id only exists after creation, so files picked during creation
    // are staged client-side and uploaded now, in the same submit action.
    for (const file of pendingFiles) {
      await upload.mutateAsync({ assignmentId: saved.id, file });
    }

    onOpenChange(false);
  }

  async function onFilePicked(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    if (isEdit && assignment) {
      await upload.mutateAsync({ assignmentId: assignment.id, file });
    } else {
      setPendingFiles((prev) => [...prev, file]);
    }
  }

  const options = mappings.data ?? [];

  // An admin's list spans every teacher, so the option label has to say whose class it is;
  // a teacher's list is all their own, where the name would be noise.
  const showsTeacherNames = new Set(options.map((option) => option.teacherId)).size > 1;

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {readOnly ? 'Assignment details' : isEdit ? 'Edit assignment' : 'New assignment'}
          </DialogTitle>
          <DialogDescription>
            {readOnly
              ? 'Read-only view — only teachers can change assignments.'
              : isEdit
                ? 'Once a published assignment has submissions, only its description can change.'
                : 'It is created as a draft — publish it when you are ready for students to see it.'}
          </DialogDescription>
        </DialogHeader>

        {!isEdit && options.length === 0 && !mappings.isLoading && (
          <Alert>
            <AlertDescription>
              You are not assigned to a class and course yet. An administrator needs to add a
              teaching assignment before you can create work.
            </AlertDescription>
          </Alert>
        )}

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="teachingMappingId">Class and course</Label>
            <Select
              value={form.watch('teachingMappingId')}
              onValueChange={(value) => form.setValue('teachingMappingId', value, { shouldValidate: true })}
              disabled={readOnly || isEdit || options.length === 0}
            >
              <SelectTrigger id="teachingMappingId" className="w-full">
                <SelectValue placeholder={mappings.isLoading ? 'Loading…' : 'Choose class and course'} />
              </SelectTrigger>
              <SelectContent>
                {options.map((mapping) => (
                  <SelectItem key={mapping.id} value={mapping.id}>
                    {mapping.className} · {mapping.courseName}
                    {/* Shown only when the list spans teachers, i.e. for an admin. */}
                    {showsTeacherNames && ` · ${mapping.teacherName}`}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isEdit && (
              <p className="text-xs text-muted-foreground">
                The class and course cannot be moved after creation.
              </p>
            )}
            {errors.teachingMappingId && (
              <p className="text-xs text-danger">{errors.teachingMappingId.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="title">Title</Label>
            <Input id="title" placeholder="Algebra fundamentals" disabled={readOnly} {...form.register('title')} />
            {errors.title && <p className="text-xs text-danger">{errors.title.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Instructions</Label>
            <Textarea
              id="description"
              rows={4}
              placeholder="What should students do, and how will it be marked?"
              disabled={readOnly}
              {...form.register('description')}
            />
            {errors.description && <p className="text-xs text-danger">{errors.description.message}</p>}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="deadlineLocal">Deadline</Label>
              <Input id="deadlineLocal" type="datetime-local" disabled={readOnly} {...form.register('deadlineLocal')} />
              {errors.deadlineLocal && (
                <p className="text-xs text-danger">{errors.deadlineLocal.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="maxMarks">Maximum marks</Label>
              <Input id="maxMarks" type="number" min={1} step="0.5" disabled={readOnly} {...form.register('maxMarks')} />
              {errors.maxMarks && <p className="text-xs text-danger">{errors.maxMarks.message}</p>}
            </div>
          </div>

          <label
            className={cn(
              'flex items-start gap-3 rounded-lg border p-3',
              readOnly ? 'cursor-default opacity-90' : 'cursor-pointer',
            )}
          >
            <input
              type="checkbox"
              className="mt-0.5 size-4 accent-primary"
              disabled={readOnly}
              {...form.register('allowResubmission')}
            />
            <span className="space-y-0.5">
              <span className="block text-sm font-medium">Allow late resubmission</span>
              <span className="block text-xs text-muted-foreground">
                Students may still update their answer after the deadline; it is recorded as late.
              </span>
            </span>
          </label>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label>Materials for students</Label>
              <span className="text-xs text-muted-foreground">
                {attachmentCount} of {MAX_FILES} · max 10 MB each
              </span>
            </div>

            {isEdit
              ? files.length > 0 && (
                  <ul className="divide-y rounded-lg border">
                    {files.map((file) => (
                      <li key={file.id} className="flex items-center gap-3 px-3 py-2">
                        <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm">{file.originalFileName}</p>
                          <p className="text-xs text-muted-foreground">{formatBytes(file.fileSizeBytes)}</p>
                        </div>
                        <Button
                          type="button"
                          size="icon"
                          variant="ghost"
                          onClick={() => downloadAssignmentFile(file.id, file.originalFileName)}
                          aria-label={`Download ${file.originalFileName}`}
                        >
                          <Download className="size-4" />
                        </Button>
                        {!readOnly && (
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            disabled={removeFile.isPending}
                            onClick={() => removeFile.mutate(file.id)}
                            aria-label={`Remove ${file.originalFileName}`}
                          >
                            <Trash2 className="size-4 text-danger" />
                          </Button>
                        )}
                      </li>
                    ))}
                  </ul>
                )
              : pendingFiles.length > 0 && (
                  <ul className="divide-y rounded-lg border">
                    {pendingFiles.map((file, index) => (
                      <li key={`${file.name}-${index}`} className="flex items-center gap-3 px-3 py-2">
                        <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm">{file.name}</p>
                          <p className="text-xs text-muted-foreground">{formatBytes(file.size)}</p>
                        </div>
                        <Button
                          type="button"
                          size="icon"
                          variant="ghost"
                          onClick={() => setPendingFiles((prev) => prev.filter((_, i) => i !== index))}
                          aria-label={`Remove ${file.name}`}
                        >
                          <Trash2 className="size-4 text-danger" />
                        </Button>
                      </li>
                    ))}
                  </ul>
                )}

            {!readOnly && (
              <>
                <input
                  ref={fileInput}
                  type="file"
                  hidden
                  accept={ALLOWED_EXTENSIONS.join(',')}
                  onChange={onFilePicked}
                />
                <Button
                  type="button"
                  variant="outline"
                  className="w-full"
                  disabled={upload.isPending || attachmentCount >= MAX_FILES}
                  onClick={() => fileInput.current?.click()}
                >
                  {upload.isPending ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
                  {attachmentCount >= MAX_FILES ? 'Attachment limit reached' : 'Attach a file'}
                </Button>
              </>
            )}
            {!isEdit && pendingFiles.length > 0 && (
              <p className="text-xs text-muted-foreground">
                Attached once you create the assignment.
              </p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Close
            </Button>
            {!readOnly && (
              <Button type="submit" disabled={save.isPending || upload.isPending}>
                {(save.isPending || upload.isPending) && <Loader2 className="size-4 animate-spin" />}
                {isEdit ? 'Save changes' : 'Create draft'}
              </Button>
            )}
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
