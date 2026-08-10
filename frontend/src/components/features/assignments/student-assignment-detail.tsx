'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import {
  Award,
  ClipboardList,
  FileText,
  Info,
  Loader2,
  MessageSquareQuote,
  Paperclip,
  PenLine,
  Upload,
} from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { RichText } from '@/components/ui/rich-text';
import { DetailSkeleton, Fact, FileRow } from '@/components/shared/detail';
import { BackLink, PageHeader } from '@/components/shared/page-header';
import { SectionPanel } from '@/components/shared/section-panel';
import { EmptyState, ErrorState } from '@/components/shared/states';
import {
  DeadlineBadge,
  NotStartedBadge,
  SubmissionStatusBadge,
} from '@/components/shared/status-badge';
import { downloadAssignmentFile } from '@/hooks/use-assignments';
import {
  downloadSubmissionFile,
  useDeleteSubmissionFile,
  useStudentAssignment,
  useSubmitAssignment,
  useUploadSubmissionFile,
} from '@/hooks/use-submissions';
import { ApiError } from '@/lib/api';
import {
  classLabel,
  deadlineUrgency,
  formatDateTime,
  formatMarks,
  formatRelative,
  gradeLabel,
  sectionLabel,
} from '@/lib/format';
import { isRichTextEmpty } from '@/lib/rich-text';
import type { StudentAssignment } from '@/types/api';

/** UX-only mirror of FileStorage:AllowedExtensions; the server re-checks the bytes. */
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];
const MAX_FILES = 3;
/** UX-only mirror of FileStorage:MaxBytes — picking is deferred, so catch this early. */
const MAX_BYTES = 2 * 1024 * 1024;

/**
 * One assignment, in full: the brief a student reads and the files they hand back.
 * A page rather than a dialog because this is the work itself — it deserves the room,
 * a URL the student can return to, and the browser's own back button.
 *
 * Writing is the teacher's side of this screen: the rich-text editor lives in the
 * assignment form, and a student only ever *reads* the brief it produces. So the
 * student's half of the page is attachments — no editor, nothing to compose.
 */
export function StudentAssignmentDetail({ assignmentId }: { assignmentId: string }) {
  const { assignment, isLoading, isError, error } = useStudentAssignment(assignmentId);

  if (isError) {
    // The API answers 404 for anything the student may not see, so a missing assignment
    // and an unpublished one are deliberately indistinguishable here.
    const missing = error instanceof ApiError && (error.status === 404 || error.status === 403);

    return (
      <div className="space-y-6">
        <BackLink href="/assignments" label="All assignments" />
        {missing ? (
          <EmptyState
            icon={ClipboardList}
            className="panel"
            title="This assignment is not available"
            description="It may have been withdrawn, or it was never published for your class."
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

  return <Detail assignment={assignment} />;
}

function Detail({ assignment }: { assignment: StudentAssignment }) {
  const submission = assignment.submission;
  const files = submission?.files ?? [];

  // Picked files are held here and only sent when the student submits — selecting a
  // file is not itself handing in, so nothing reaches the server until they say so.
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const fileInput = useRef<HTMLInputElement>(null);

  const submit = useSubmitAssignment();
  const upload = useUploadSubmissionFile();
  const removeFile = useDeleteSubmissionFile();

  // A typed answer can only exist on a submission made before this screen dropped its
  // editor. It is shown back read-only and carried through every save, so nothing a
  // student wrote is quietly thrown away.
  const savedAnswer = submission?.content ?? '';
  const hasSavedAnswer = !isRichTextEmpty(savedAnswer);
  const urgency = deadlineUrgency(assignment.deadlineUtc);
  const isGraded = submission?.status === 'Graded';
  const readOnly = isGraded || (urgency === 'overdue' && !assignment.allowResubmission);
  const attachmentCount = files.length + pendingFiles.length;
  // An upload creates the submission row but leaves it Pending, so "handed in" is a
  // question about status, not about whether files exist.
  const handedIn = !!submission && submission.status !== 'Pending';
  // Nothing to send once it is in and no new file is waiting — re-posting would only
  // re-stamp the submitted time.
  const canHandIn = attachmentCount > 0 && (!handedIn || pendingFiles.length > 0);
  const isBusy = submit.isPending || upload.isPending;
  const hasInstructions = !isRichTextEmpty(assignment.description);

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

  async function onSubmit() {
    try {
      // Files go first, and deliberately so: the server refuses a submission that has
      // neither text nor a file, and the upload endpoint is what creates the submission
      // row. With no answer to send, uploading is what makes there be something to hand
      // in. Each file leaves the staging list as it lands, so a retry after a failure
      // part-way through does not send the same file twice.
      for (const file of pendingFiles) {
        await upload.mutateAsync({ assignmentId: assignment.id, file });
        setPendingFiles((prev) => prev.filter((staged) => staged !== file));
      }

      // Uploading leaves the submission Pending; this call is what hands it in. Any
      // answer written before the editor went away rides along unchanged.
      await submit.mutateAsync({
        assignmentId: assignment.id,
        submissionId: submission?.id,
        content: hasSavedAnswer ? savedAnswer : '',
      });
    } catch {
      // The mutations already report the failure; staged files stay put so the student
      // can retry without picking them again.
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/assignments', label: 'All assignments' }}
        eyebrow={`${assignment.courseCode} · ${assignment.courseName}`}
        title={assignment.title}
        description={`Set by ${assignment.teacherName} for ${classLabel(assignment.classLevel, assignment.classSection)}`}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <DeadlineBadge urgency={urgency}>
              {urgency === 'overdue' ? 'Closed' : 'Due'} {formatRelative(assignment.deadlineUtc)}
            </DeadlineBadge>
            {submission ? <SubmissionStatusBadge status={submission.status} /> : <NotStartedBadge />}
          </div>
        }
      />

      <div className="grid gap-6 lg:grid-cols-3 lg:items-start">
        <div className="space-y-6 lg:col-span-2">
          {/* The brief, exactly as the teacher wrote it. Framed as a sheet inside the
              panel so it reads as a document being shown rather than a field. */}
          <SectionPanel
            title="Instructions from your teacher"
            description="What you have been asked to do"
            icon={FileText}
            bodyClassName="p-5"
          >
            {hasInstructions ? (
              <div className="rounded-xl border bg-muted/30 p-5">
                <RichText content={assignment.description} />
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">
                Your teacher has not written any instructions for this assignment — check any
                materials they attached, or ask them directly.
              </p>
            )}
          </SectionPanel>

          {assignment.files.length > 0 && (
            <SectionPanel
              title="Materials from your teacher"
              description={`${assignment.files.length} file${assignment.files.length > 1 ? 's' : ''}`}
              icon={Paperclip}
              bodyClassName="divide-y"
            >
              {assignment.files.map((file) => (
                <FileRow
                  key={file.id}
                  name={file.originalFileName}
                  size={file.fileSizeBytes}
                  onDownload={() => downloadAssignmentFile(file.id, file.originalFileName)}
                />
              ))}
            </SectionPanel>
          )}

          {isGraded && (
            <section className="panel space-y-3 border-success/25 bg-success-muted/40 p-5">
              <div className="flex items-center gap-2">
                <Award className="size-5 text-success" />
                <h2 className="font-heading text-sm font-semibold">
                  Marked: {formatMarks(submission.marks, submission.marksOutOf)}
                </h2>
              </div>
              {submission.feedback && (
                <p className="flex gap-2 text-sm text-muted-foreground">
                  <MessageSquareQuote className="mt-0.5 size-4 shrink-0" />
                  <span className="whitespace-pre-wrap">{submission.feedback}</span>
                </p>
              )}
              {submission.reviewedByName && (
                <p className="text-xs text-muted-foreground">
                  by {submission.reviewedByName} · {formatDateTime(submission.reviewedAtUtc)}
                </p>
              )}
            </section>
          )}

          {/* Only ever seen by a student who typed an answer while this screen still had
              an editor — shown the same way the teacher marking it sees it. */}
          {hasSavedAnswer && (
            <SectionPanel
              title="Your written answer"
              description="Submitted earlier · read-only"
              icon={PenLine}
              bodyClassName="p-5"
            >
              <RichText content={savedAnswer} />
            </SectionPanel>
          )}
        </div>

        {/* Two panels now ride the rail, which together can outgrow the viewport — so it
            keeps its own scroll rather than pinning the facts half off-screen. */}
        <aside className="space-y-6 lg:sticky lg:top-20 lg:max-h-[calc(100vh-6rem)] lg:overflow-y-auto">
          {/* Handing in is the one thing a student *does* here, so it sits at the top of
              the rail — above the facts, in reach without scrolling the brief. */}
          <SectionPanel
            title="Your submission"
            description={
              readOnly
                ? 'Closed for changes'
                : `${attachmentCount} of ${MAX_FILES} attached · max 2 MB each`
            }
            icon={Paperclip}
            bodyClassName="space-y-3 p-5"
          >
            {readOnly && (
              <Alert>
                <AlertDescription>
                  {isGraded
                    ? 'This submission has been marked and can no longer be changed.'
                    : 'The deadline has passed and this assignment does not allow late changes.'}
                </AlertDescription>
              </Alert>
            )}

            {attachmentCount > 0 ? (
              <div className="divide-y rounded-lg border">
                {files.map((file) => (
                  <FileRow
                    key={file.id}
                    name={file.originalFileName}
                    size={file.fileSizeBytes}
                    onDownload={() => downloadSubmissionFile(file.id, file.originalFileName)}
                    onRemove={readOnly ? undefined : () => removeFile.mutate(file.id)}
                    removeDisabled={removeFile.isPending}
                  />
                ))}
                {/* Staged picks: no id and nothing to download yet, so they read as
                    pending until the student hands in. */}
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
              </div>
            ) : (
              !readOnly && (
                <p className="text-sm text-muted-foreground">
                  Attach your work as a file, then hand it in.
                </p>
              )
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
                <Button className="w-full" onClick={onSubmit} disabled={isBusy || !canHandIn}>
                  {isBusy && <Loader2 className="size-4 animate-spin" />}
                  {!handedIn ? 'Hand in' : pendingFiles.length > 0 ? 'Update submission' : 'Handed in'}
                </Button>
                <p className="text-xs text-muted-foreground">
                  {pendingFiles.length > 0
                    ? 'Sent when you hand in.'
                    : `Allowed: ${ALLOWED_EXTENSIONS.join(', ')}`}
                </p>
              </>
            )}
          </SectionPanel>

          <SectionPanel title="At a glance" icon={Info} bodyClassName="divide-y">
            <Fact label="Status">
              {submission ? <SubmissionStatusBadge status={submission.status} /> : <NotStartedBadge />}
            </Fact>
            <Fact label="Deadline">
              <span className="block">{formatDateTime(assignment.deadlineUtc)}</span>
              <span className="block text-xs font-normal text-muted-foreground">
                {formatRelative(assignment.deadlineUtc)}
              </span>
            </Fact>
            <Fact label={isGraded ? 'Marks' : 'Out of'}>
              <span className="tabular-nums">
                {isGraded
                  ? formatMarks(submission.marks, submission.marksOutOf)
                  : assignment.maxMarks}
              </span>
            </Fact>
            <Fact label="Submitted">{formatDateTime(submission?.submittedAtUtc)}</Fact>
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
          </SectionPanel>
        </aside>
      </div>
    </div>
  );
}
