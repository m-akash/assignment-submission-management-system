'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { CalendarClock, FileText, Loader2, Paperclip, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Combobox } from '@/components/ui/combobox';
import { DetailSkeleton, FileRow } from '@/components/shared/detail';
import { PageHeader } from '@/components/shared/page-header';
import { SectionPanel } from '@/components/shared/section-panel';
import {
  downloadAssignmentFile,
  useDeleteAssignmentFile,
  useSaveAssignment,
  useUploadAssignmentFile,
} from '@/hooks/use-assignments';
import { useMyTeacherMappings } from '@/hooks/use-admin-resources';
import { classLabel } from '@/lib/format';
import { cn } from '@/lib/utils';
import { assignmentSchema, type AssignmentInput, type AssignmentValues } from '@/schemas';
import type { Assignment, TeacherMapping } from '@/types/api';

/** UX-only mirror of FileStorage:AllowedExtensions; the server re-checks the bytes. */
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];
const MAX_FILES = 5;
/** UX-only mirror of FileStorage:MaxBytes — picking is deferred, so catch this early. */
const MAX_BYTES = 2 * 1024 * 1024;

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

/**
 * Writing an assignment: a page, like reading one. The brief is the substance of the
 * work rather than a detail to confirm, and it is authored in a rich-text editor with
 * files alongside it — more than a dialog can hold without becoming a scroll box.
 *
 * Both halves of the lifecycle live here: an assignment is written once and revised
 * afterwards through the same fields, so create and edit are one form told apart by
 * whether an assignment was handed in.
 */
export function AssignmentFormView({ assignment }: { assignment?: Assignment | null }) {
  // Waited on rather than reset into: the offering picker and, on edit, its selected
  // value both come from this list, and the editor seeds its document once at mount.
  const mappings = useMyTeacherMappings();

  if (mappings.isLoading) return <DetailSkeleton />;

  return <Form assignment={assignment ?? null} options={mappings.data ?? []} />;
}

function Form({
  assignment,
  options,
}: {
  assignment: Assignment | null;
  options: TeacherMapping[];
}) {
  const router = useRouter();
  const isEdit = !!assignment;
  const backHref = assignment ? `/assignments/${assignment.id}` : '/assignments';

  const save = useSaveAssignment();
  const upload = useUploadAssignmentFile();
  const removeFile = useDeleteAssignmentFile();

  const fileInput = useRef<HTMLInputElement>(null);
  const files = assignment?.files ?? [];
  // Picked files wait here until the form is submitted: on create there is no
  // assignment id to upload against yet, and on edit a pick is not a decision to
  // change the assignment — leaving without saving must leave the material alone.
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const attachmentCount = files.length + pendingFiles.length;

  // <what the fields hold, context, what validation produces> — `maxMarks` is coerced,
  // so the first and last are not the same type.
  const form = useForm<AssignmentInput, unknown, AssignmentValues>({
    resolver: zodResolver(assignmentSchema),
    defaultValues: assignment
      ? {
          // Resolved from the assignment's offering and author rather than stored on it:
          // the assignment points at the offering directly, not at a mapping.
          teachingMappingId: mappingFor(assignment, options)?.id ?? '',
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
  });

  const errors = form.formState.errors;
  const isBusy = save.isPending || upload.isPending;

  // An admin's list spans every teacher, so the option label has to say whose class it is;
  // a teacher's list is all their own, where the name would be noise.
  const showsTeacherNames = new Set(options.map((option) => option.teacherId)).size > 1;

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

    // Staged picks go up now, in the same submit action — on create this is the first
    // moment an id exists. Each file leaves the staging list as it lands, so a retry
    // after a failure part-way through does not send the same file twice.
    for (const file of pendingFiles) {
      await upload.mutateAsync({ assignmentId: saved.id, file });
      setPendingFiles((prev) => prev.filter((staged) => staged !== file));
    }

    // Straight to the finished thing, which is what the author wants to check.
    router.push(`/assignments/${saved.id}`);
  }

  function onFilePicked(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    if (file.size > MAX_BYTES) {
      toast.error(`${file.name} is larger than 2 MB.`);
      return;
    }

    setPendingFiles((prev) => [...prev, file]);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        back={{
          href: backHref,
          label: isEdit ? `Back to ${assignment.title}` : 'All assignments',
        }}
        eyebrow="Coursework"
        title={isEdit ? 'Edit assignment' : 'New assignment'}
        description={
          isEdit
            ? 'Once a published assignment has submissions, only its description can change.'
            : 'It is created as a draft — publish it when you are ready for students to see it.'
        }
      />

      {!isEdit && options.length === 0 && (
        <Alert>
          <AlertDescription>
            You are not assigned to a class and course yet. An administrator needs to add a
            teaching assignment before you can create work.
          </AlertDescription>
        </Alert>
      )}

      <form onSubmit={form.handleSubmit(onSubmit)} noValidate>
        <div className="grid gap-6 lg:grid-cols-3 lg:items-start">
          <div className="space-y-6 lg:col-span-2">
            <SectionPanel title="The work" icon={FileText} bodyClassName="space-y-4 p-5">
              <div className="space-y-2">
                <Label htmlFor="teachingMappingId">Class and course</Label>
                <Controller
                  control={form.control}
                  name="teachingMappingId"
                  render={({ field }) => (
                    <Combobox
                      id="teachingMappingId"
                      value={field.value}
                      onChange={field.onChange}
                      options={options.map((mapping) => ({
                        value: mapping.id,
                        label: `${classLabel(mapping.classLevel, mapping.classSection)} · ${mapping.courseName}`,
                        // Shown only when the list spans teachers, i.e. for an admin.
                        hint: showsTeacherNames ? mapping.teacherName : undefined,
                      }))}
                      placeholder="Choose class and course"
                      searchPlaceholder="Search grade, section or course…"
                      emptyMessage="No offerings match"
                      disabled={isEdit || options.length === 0}
                      aria-invalid={!!errors.teachingMappingId}
                      className="w-full"
                    />
                  )}
                />
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
                <Input id="title" placeholder="Algebra fundamentals" {...form.register('title')} />
                {errors.title && <p className="text-xs text-danger">{errors.title.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Instructions</Label>
                {/* Controlled rather than registered: the editor's value is HTML it builds
                    itself, not something a DOM ref can be read off. */}
                <Controller
                  control={form.control}
                  name="description"
                  render={({ field }) => (
                    <RichTextEditor
                      id="description"
                      value={field.value}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      invalid={!!errors.description}
                      placeholder="What should students do, and how will it be marked?"
                    />
                  )}
                />
                {errors.description && (
                  <p className="text-xs text-danger">{errors.description.message}</p>
                )}
              </div>
            </SectionPanel>

            <SectionPanel
              title="Materials for students"
              description={`${attachmentCount} of ${MAX_FILES} · max 2 MB each`}
              icon={Paperclip}
              bodyClassName={attachmentCount > 0 ? 'divide-y' : undefined}
            >
              {files.map((file) => (
                <FileRow
                  key={file.id}
                  name={file.originalFileName}
                  size={file.fileSizeBytes}
                  onDownload={() => downloadAssignmentFile(file.id, file.originalFileName)}
                  onRemove={() => removeFile.mutate(file.id)}
                  removeDisabled={removeFile.isPending}
                />
              ))}
              {/* Staged picks: no id and nothing to download yet, so they read as
                  pending until the form is saved. */}
              {pendingFiles.map((file, index) => (
                <FileRow
                  key={`${file.name}-${index}`}
                  name={file.name}
                  size={file.size}
                  hint="Pending"
                  onRemove={() => setPendingFiles((prev) => prev.filter((_, i) => i !== index))}
                  removeDisabled={isBusy}
                />
              ))}

              <div className="space-y-2 p-5 pt-4">
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
                  disabled={isBusy || attachmentCount >= MAX_FILES}
                  onClick={() => fileInput.current?.click()}
                >
                  {upload.isPending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Upload className="size-4" />
                  )}
                  {attachmentCount >= MAX_FILES ? 'Attachment limit reached' : 'Attach a file'}
                </Button>
                <p className="text-xs text-muted-foreground">
                  {pendingFiles.length > 0
                    ? isEdit
                      ? 'Attached once you save your changes.'
                      : 'Attached once you create the assignment.'
                    : `Allowed: ${ALLOWED_EXTENSIONS.join(', ')}`}
                </p>
              </div>
            </SectionPanel>
          </div>

          <aside className="space-y-6 lg:sticky lg:top-20">
            <SectionPanel
              title="Deadline and marking"
              icon={CalendarClock}
              bodyClassName="space-y-4 p-5"
            >
              <div className="space-y-2">
                <Label htmlFor="deadlineLocal">Deadline</Label>
                <Input
                  id="deadlineLocal"
                  type="datetime-local"
                  {...form.register('deadlineLocal')}
                />
                {errors.deadlineLocal && (
                  <p className="text-xs text-danger">{errors.deadlineLocal.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="maxMarks">Maximum marks</Label>
                <Input id="maxMarks" type="number" min={1} step="0.5" {...form.register('maxMarks')} />
                {errors.maxMarks && <p className="text-xs text-danger">{errors.maxMarks.message}</p>}
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
                    Students may still update their answer after the deadline; it is recorded as
                    late.
                  </span>
                </span>
              </label>
            </SectionPanel>

            <div
              className={cn(
                'panel flex flex-col-reverse gap-2 p-4',
                'sm:flex-row sm:items-center sm:justify-end lg:flex-col-reverse lg:items-stretch',
              )}
            >
              <Button asChild type="button" variant="outline" disabled={isBusy}>
                <Link href={backHref}>Cancel</Link>
              </Button>
              <Button type="submit" disabled={isBusy || (!isEdit && options.length === 0)}>
                {isBusy && <Loader2 className="size-4 animate-spin" />}
                {isEdit ? 'Save changes' : 'Create draft'}
              </Button>
            </div>
          </aside>
        </div>
      </form>
    </div>
  );
}

/**
 * The mapping that corresponds to an existing assignment: same offering, same author.
 * Falls back to any mapping for the offering so the disabled picker still shows the right
 * class and course even if the admin has since reassigned who teaches it.
 */
function mappingFor(assignment: Assignment, options: TeacherMapping[]) {
  return (
    options.find(
      (option) =>
        option.classCourseId === assignment.classCourseId &&
        option.teacherId === assignment.teacherId,
    ) ?? options.find((option) => option.classCourseId === assignment.classCourseId)
  );
}
