'use client';

import { useEffect, useRef } from 'react';
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
import { assignmentSchema, type AssignmentValues } from '@/schemas';
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
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  assignment?: Assignment | null;
}) {
  const isEdit = !!assignment;
  const mappings = useMyTeacherMappings(open);
  const save = useSaveAssignment();
  const upload = useUploadAssignmentFile();
  const removeFile = useDeleteAssignmentFile();
  const fileInput = useRef<HTMLInputElement>(null);
  const files = assignment?.files ?? [];

  const form = useForm<AssignmentValues>({
    resolver: zodResolver(assignmentSchema),
    defaultValues: {
      teacherAssignmentId: '',
      title: '',
      description: '',
      deadlineLocal: defaultDeadline(),
      maxMarks: 100,
      allowResubmission: true,
    },
  });

  // Repopulate whenever the dialog opens so a reopened form never shows stale values.
  useEffect(() => {
    if (!open) return;
    form.reset(
      assignment
        ? {
            teacherAssignmentId: assignment.teacherAssignmentId,
            title: assignment.title,
            description: assignment.description,
            deadlineLocal: toLocalInput(assignment.deadlineUtc),
            maxMarks: assignment.maxMarks,
            allowResubmission: assignment.allowResubmission,
          }
        : {
            teacherAssignmentId: '',
            title: '',
            description: '',
            deadlineLocal: defaultDeadline(),
            maxMarks: 100,
            allowResubmission: true,
          },
    );
  }, [open, assignment, form]);

  async function onSubmit(values: AssignmentValues) {
    await save.mutateAsync({
      id: assignment?.id,
      input: {
        teacherAssignmentId: values.teacherAssignmentId,
        title: values.title,
        description: values.description,
        // The API stores UTC; convert once, here.
        deadlineUtc: new Date(values.deadlineLocal).toISOString(),
        maxMarks: values.maxMarks,
        allowResubmission: values.allowResubmission,
      },
    });
    onOpenChange(false);
  }

  async function onFilePicked(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file || !assignment) return;

    await upload.mutateAsync({ assignmentId: assignment.id, file });
  }

  const options = mappings.data ?? [];
  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit assignment' : 'New assignment'}</DialogTitle>
          <DialogDescription>
            {isEdit
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
            <Label htmlFor="teacherAssignmentId">Class and course</Label>
            <Select
              value={form.watch('teacherAssignmentId')}
              onValueChange={(value) => form.setValue('teacherAssignmentId', value, { shouldValidate: true })}
              disabled={isEdit || options.length === 0}
            >
              <SelectTrigger id="teacherAssignmentId" className="w-full">
                <SelectValue placeholder={mappings.isLoading ? 'Loading…' : 'Choose class and course'} />
              </SelectTrigger>
              <SelectContent>
                {options.map((mapping) => (
                  <SelectItem key={mapping.id} value={mapping.id}>
                    {mapping.className} · {mapping.courseName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isEdit && (
              <p className="text-xs text-muted-foreground">
                The class and course cannot be moved after creation.
              </p>
            )}
            {errors.teacherAssignmentId && (
              <p className="text-xs text-danger">{errors.teacherAssignmentId.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="title">Title</Label>
            <Input id="title" placeholder="Algebra fundamentals" {...form.register('title')} />
            {errors.title && <p className="text-xs text-danger">{errors.title.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Instructions</Label>
            <Textarea
              id="description"
              rows={4}
              placeholder="What should students do, and how will it be marked?"
              {...form.register('description')}
            />
            {errors.description && <p className="text-xs text-danger">{errors.description.message}</p>}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="deadlineLocal">Deadline</Label>
              <Input id="deadlineLocal" type="datetime-local" {...form.register('deadlineLocal')} />
              {errors.deadlineLocal && (
                <p className="text-xs text-danger">{errors.deadlineLocal.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="maxMarks">Maximum marks</Label>
              <Input id="maxMarks" type="number" min={1} step="0.5" {...form.register('maxMarks')} />
              {errors.maxMarks && <p className="text-xs text-danger">{errors.maxMarks.message}</p>}
            </div>
          </div>

          <label className="flex cursor-pointer items-start gap-3 rounded-lg border p-3">
            <input
              type="checkbox"
              className="mt-0.5 size-4 accent-primary"
              {...form.register('allowResubmission')}
            />
            <span className="space-y-0.5">
              <span className="block text-sm font-medium">Allow late resubmission</span>
              <span className="block text-xs text-muted-foreground">
                Students may still update their answer after the deadline; it is recorded as late.
              </span>
            </span>
          </label>

          {isEdit && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>Materials for students</Label>
                <span className="text-xs text-muted-foreground">
                  {files.length} of {MAX_FILES} · max 10 MB each
                </span>
              </div>

              {files.length > 0 && (
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
                    </li>
                  ))}
                </ul>
              )}

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
                disabled={upload.isPending || files.length >= MAX_FILES}
                onClick={() => fileInput.current?.click()}
              >
                {upload.isPending ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
                {files.length >= MAX_FILES ? 'Attachment limit reached' : 'Attach a file'}
              </Button>
            </div>
          )}
          {!isEdit && (
            <p className="text-xs text-muted-foreground">
              You can attach reference material once the assignment is created.
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create draft'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
