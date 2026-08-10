'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  ClipboardList,
  FileText,
  Inbox,
  Info,
  Loader2,
  Paperclip,
  Pencil,
  Send,
  Trash2,
  Upload,
} from 'lucide-react';
import { toast } from 'sonner';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { RichText } from '@/components/ui/rich-text';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { DetailSkeleton, Fact, FileRow } from '@/components/shared/detail';
import { ImagePreviewDialog, isViewableImage } from '@/components/shared/file-preview';
import { BackLink, PageHeader } from '@/components/shared/page-header';
import { SectionPanel } from '@/components/shared/section-panel';
import { EmptyState, ErrorState } from '@/components/shared/states';
import {
  AssignmentStatusBadge,
  DeadlineBadge,
  SubmissionStatusBadge,
} from '@/components/shared/status-badge';
import {
  downloadAssignmentFile,
  fetchAssignmentFile,
  useAssignment,
  useDeleteAssignment,
  useDeleteAssignmentFile,
  usePublishAssignment,
  useUploadAssignmentFile,
} from '@/hooks/use-assignments';
import { useSubmissions } from '@/hooks/use-submissions';
import { ApiError } from '@/lib/api';
import {
  classLabel,
  deadlineUrgency,
  formatDateTime,
  formatMarks,
  formatRelative,
  gradeLabel,
  initials,
  sectionLabel,
} from '@/lib/format';
import type { Assignment, AssignmentFile } from '@/types/api';

/** UX-only mirror of FileStorage:AllowedExtensions; the server re-checks the bytes. */
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];
const MAX_FILES = 5;
/** UX-only mirror of FileStorage:MaxBytes — picking is deferred, so catch this early. */
const MAX_BYTES = 2 * 1024 * 1024;

/** How many submissions the panel shows before deferring to the full inbox. */
const PREVIEW_COUNT = 8;

/**
 * One assignment as its author sees it: the brief, the material students receive, and
 * who has handed in. The counterpart to the student's page — same layout, different
 * question, since a teacher opens an assignment to see how the class is doing with it.
 */
export function TeacherAssignmentDetail({
  assignmentId,
  readOnly = false,
}: {
  assignmentId: string;
  /** Admins browse Coursework read-only — no editing, publishing or deleting. */
  readOnly?: boolean;
}) {
  const { data: assignment, isLoading, isError, error } = useAssignment(assignmentId);

  if (isError) {
    const missing = error instanceof ApiError && (error.status === 404 || error.status === 403);

    return (
      <div className="space-y-6">
        <BackLink href="/assignments" label="All assignments" />
        {missing ? (
          <EmptyState
            icon={ClipboardList}
            className="panel"
            title="This assignment is not available"
            description="It may have been deleted, or it belongs to a teacher other than you."
            action={
              <Button asChild size="sm" variant="outline">
                <Link href="/assignments">Back to assignments</Link>
              </Button>
            }
          />
        ) : (
          <ErrorState
            className="panel"
            title="Could not load this assignment"
            message={error instanceof Error ? error.message : undefined}
          />
        )}
      </div>
    );
  }

  if (isLoading || !assignment) return <DetailSkeleton />;

  return <Detail assignment={assignment} readOnly={readOnly} />;
}

function Detail({ assignment, readOnly }: { assignment: Assignment; readOnly: boolean }) {
  const router = useRouter();

  const [confirmingDelete, setConfirmingDelete] = useState(false);
  // The material being viewed inline, if any — the teacher sees an image the same way
  // the class will.
  const [viewing, setViewing] = useState<AssignmentFile | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const publish = usePublishAssignment();
  const remove = useDeleteAssignment();
  const upload = useUploadAssignmentFile();
  const removeFile = useDeleteAssignmentFile();

  // Every submission for this assignment: the panel below shows the first few, and the
  // marked/awaiting split needs the whole set rather than one page of it. A single
  // class hands in tens of pieces, not thousands, so one generous page is enough.
  const submissions = useSubmissions({ assignmentId: assignment.id, pageSize: 100 });
  const handedIn = submissions.data?.items ?? [];
  const marked = handedIn.filter((item) => item.status === 'Graded').length;
  const awaiting = handedIn.length - marked;

  const urgency = deadlineUrgency(assignment.deadlineUtc);
  const isDraft = assignment.status === 'Draft';
  const isBusy = publish.isPending || remove.isPending;

  function onFilePicked(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    if (file.size > MAX_BYTES) {
      toast.error(`${file.name} is larger than 2 MB.`);
      return;
    }

    // The assignment already exists here, so a pick is an upload — unlike the create
    // form, which has to stage files until there is an id to hang them off.
    upload.mutate({ assignmentId: assignment.id, file });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/assignments', label: 'All assignments' }}
        eyebrow={`${assignment.courseCode} · ${assignment.courseName}`}
        title={assignment.title}
        description={
          readOnly
            ? `Set by ${assignment.teacherName} for ${classLabel(assignment.classLevel, assignment.classSection)}`
            : `For ${classLabel(assignment.classLevel, assignment.classSection)} · ${assignment.maxMarks} marks`
        }
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <AssignmentStatusBadge status={assignment.status} />
            <DeadlineBadge urgency={urgency}>
              {urgency === 'overdue' ? 'Closed' : 'Due'} {formatRelative(assignment.deadlineUtc)}
            </DeadlineBadge>

            {!readOnly && (
              <>
                {isDraft && (
                  <Button
                    size="sm"
                    disabled={isBusy}
                    onClick={() => publish.mutate(assignment.id)}
                  >
                    {publish.isPending ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Send className="size-4" />
                    )}
                    Publish
                  </Button>
                )}
                <Button asChild size="sm" variant="outline">
                  <Link href={`/assignments/${assignment.id}/edit`}>
                    <Pencil className="size-4" />
                    Edit
                  </Link>
                </Button>
                <Button
                  size="sm"
                  variant="destructive"
                  disabled={isBusy}
                  onClick={() => setConfirmingDelete(true)}
                >
                  <Trash2 className="size-4" />
                  Delete
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="grid gap-6 lg:grid-cols-3 lg:items-start">
        <div className="space-y-6 lg:col-span-2">
          <SectionPanel title="Instructions" icon={FileText} bodyClassName="p-5">
            <RichText content={assignment.description} />
          </SectionPanel>

          <SectionPanel
            title="Materials for students"
            description={
              readOnly || assignment.files.length >= MAX_FILES
                ? `${assignment.files.length} of ${MAX_FILES} files`
                : `${assignment.files.length} of ${MAX_FILES} · max 2 MB each`
            }
            icon={Paperclip}
            bodyClassName={assignment.files.length > 0 ? 'divide-y' : undefined}
          >
            {assignment.files.map((file) => (
              <FileRow
                key={file.id}
                name={file.originalFileName}
                size={file.fileSizeBytes}
                onView={
                  isViewableImage(file.contentType, file.originalFileName)
                    ? () => setViewing(file)
                    : undefined
                }
                onDownload={() => downloadAssignmentFile(file.id, file.originalFileName)}
                onRemove={readOnly ? undefined : () => removeFile.mutate(file.id)}
                removeDisabled={removeFile.isPending}
              />
            ))}

            {assignment.files.length === 0 && (
              <p className="px-5 py-4 text-sm text-muted-foreground">
                {readOnly
                  ? 'No material was attached to this assignment.'
                  : 'Nothing attached yet — add a brief, a dataset, or anything students need.'}
              </p>
            )}

            {!readOnly && (
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
                  disabled={upload.isPending || assignment.files.length >= MAX_FILES}
                  onClick={() => fileInput.current?.click()}
                >
                  {upload.isPending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Upload className="size-4" />
                  )}
                  {assignment.files.length >= MAX_FILES
                    ? 'Attachment limit reached'
                    : 'Attach a file'}
                </Button>
                <p className="text-xs text-muted-foreground">
                  Uploaded straight away — students see it as soon as the assignment is
                  published. Allowed: {ALLOWED_EXTENSIONS.join(', ')}
                </p>
              </div>
            )}
          </SectionPanel>

          <SectionPanel
            title="Submissions"
            description={
              handedIn.length > 0
                ? `${handedIn.length} handed in · ${marked} marked · ${awaiting} awaiting`
                : 'Nobody has handed in yet'
            }
            icon={Inbox}
            action={
              handedIn.length > PREVIEW_COUNT && (
                <Button asChild size="sm" variant="outline">
                  <Link href={`/submissions?assignmentId=${assignment.id}`}>
                    View all {handedIn.length}
                  </Link>
                </Button>
              )
            }
            bodyClassName={handedIn.length > 0 ? 'divide-y' : undefined}
          >
            {submissions.isError ? (
              <ErrorState
                title="Could not load submissions"
                message={
                  submissions.error instanceof Error ? submissions.error.message : undefined
                }
              />
            ) : submissions.isLoading ? (
              <p className="px-5 py-4 text-sm text-muted-foreground">Loading…</p>
            ) : handedIn.length === 0 ? (
              <EmptyState
                icon={Inbox}
                title={isDraft ? 'Not published yet' : 'No submissions yet'}
                description={
                  isDraft
                    ? 'Students cannot see a draft. Publish it when the work should begin.'
                    : 'Once students hand in, their work will be listed here for marking.'
                }
              />
            ) : (
              handedIn.slice(0, PREVIEW_COUNT).map((submission) => (
                <Link
                  key={submission.id}
                  href={`/submissions/${submission.id}`}
                  className="flex items-center gap-3 px-5 py-3 transition-colors hover:bg-muted/50"
                >
                  <Avatar className="size-7">
                    <AvatarFallback className="text-[11px]">
                      {initials(submission.studentName)}
                    </AvatarFallback>
                  </Avatar>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{submission.studentName}</p>
                    <p className="text-xs text-muted-foreground">
                      {submission.submittedAtUtc
                        ? `handed in ${formatRelative(submission.submittedAtUtc)}`
                        : 'not submitted'}
                    </p>
                  </div>
                  <SubmissionStatusBadge status={submission.status} />
                  <span className="w-16 shrink-0 text-right text-sm tabular-nums">
                    {formatMarks(submission.marks, submission.marksOutOf)}
                  </span>
                </Link>
              ))
            )}
          </SectionPanel>
        </div>

        <aside className="lg:sticky lg:top-20">
          <SectionPanel title="At a glance" icon={Info} bodyClassName="divide-y">
            <Fact label="Status">
              <AssignmentStatusBadge status={assignment.status} />
            </Fact>
            <Fact label="Deadline">
              <span className="block">{formatDateTime(assignment.deadlineUtc)}</span>
              <span className="block text-xs font-normal text-muted-foreground">
                {formatRelative(assignment.deadlineUtc)}
              </span>
            </Fact>
            <Fact label="Out of">
              <span className="tabular-nums">{assignment.maxMarks}</span>
            </Fact>
            <Fact label="Handed in">
              <span className="tabular-nums">{assignment.submissionCount}</span>
            </Fact>
            <Fact label="Late changes">
              {assignment.allowResubmission ? 'Allowed' : 'Not allowed'}
            </Fact>
            <Fact label="Course">
              <span className="block">{assignment.courseName}</span>
              <span className="block font-mono text-xs font-normal text-muted-foreground">
                {assignment.courseCode}
              </span>
            </Fact>
            <Fact label="Class">{gradeLabel(assignment.classLevel)}</Fact>
            <Fact label="Section">{sectionLabel(assignment.classSection)}</Fact>
            <Fact label="Teacher">{assignment.teacherName}</Fact>
            <Fact label="Created">{formatDateTime(assignment.createdAtUtc)}</Fact>
          </SectionPanel>
        </aside>
      </div>

      <ImagePreviewDialog
        file={
          viewing && {
            id: viewing.id,
            name: viewing.originalFileName,
            contentType: viewing.contentType,
            sizeBytes: viewing.fileSizeBytes,
          }
        }
        loadBlob={fetchAssignmentFile}
        onDownload={(file) => downloadAssignmentFile(file.id, file.name)}
        onClose={() => setViewing(null)}
      />

      {!readOnly && (
        <ConfirmDialog
          open={confirmingDelete}
          onOpenChange={setConfirmingDelete}
          title="Delete this assignment?"
          description={`"${assignment.title}" will be hidden from students. Submissions already made are kept.`}
          pending={remove.isPending}
          onConfirm={() =>
            remove.mutate(assignment.id, {
              // The record this page is about is gone, so there is nothing to return to.
              onSuccess: () => router.replace('/assignments'),
            })
          }
        />
      )}
    </div>
  );
}
