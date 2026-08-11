'use client';

import { useState } from 'react';
import Link from 'next/link';
import {
  Award,
  CalendarClock,
  CheckCheck,
  ClipboardList,
  FileText,
  History,
  Loader2,
  MessageSquareQuote,
  Paperclip,
  Target,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { RichText } from '@/components/ui/rich-text';
import {
  DetailSkeleton,
  FileRow,
  MetaBar,
  MetaDivider,
  MetaItem,
} from '@/components/shared/detail';
import { FileDropzone } from '@/components/shared/file-dropzone';
import {
  canPreview,
  FilePreviewDialog,
  type PreviewFile,
} from '@/components/shared/file-preview';
import { BackLink, PageHeader } from '@/components/shared/page-header';
import { SectionPanel } from '@/components/shared/section-panel';
import { EmptyState, ErrorState } from '@/components/shared/states';
import {
  DeadlineBadge,
  NotStartedBadge,
  SubmissionStatusBadge,
} from '@/components/shared/status-badge';
import { downloadAssignmentFile, fetchAssignmentFile } from '@/hooks/use-assignments';
import {
  downloadSubmissionFile,
  fetchSubmissionFile,
  useDeleteSubmissionFile,
  useRenameSubmissionFile,
  useStudentAssignment,
  useSubmitAssignment,
  useUploadSubmissionFile,
} from '@/hooks/use-submissions';
import { ApiError } from '@/lib/api';
import { renameFile } from '@/lib/file-name';
import {
  classLabel,
  deadlineUrgency,
  formatDateTime,
  formatMarks,
  formatRelative,
} from '@/lib/format';
import { isRichTextEmpty } from '@/lib/rich-text';
import type { AssignmentFile, StudentAssignment, SubmissionFile } from '@/types/api';

const MAX_FILES = 3;

/**
 * Whichever side of the page a file came from, the viewer wants the same four fields —
 * and the two DTOs happen to spell them identically.
 */
function toPreview(file: AssignmentFile | SubmissionFile): PreviewFile {
  return {
    id: file.id,
    name: file.originalFileName,
    contentType: file.contentType,
    sizeBytes: file.fileSizeBytes,
  };
}

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
  // The file being viewed inline, if any. Which list it came from decides which endpoint
  // the bytes are fetched from, so it is carried alongside.
  const [viewing, setViewing] = useState<{
    file: PreviewFile;
    source: 'material' | 'submission';
  } | null>(null);

  const submit = useSubmitAssignment();
  const upload = useUploadSubmissionFile();
  const removeFile = useDeleteSubmissionFile();
  const renameFileOnServer = useRenameSubmissionFile();

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

  /**
   * A staged file renamed in place. Worth having on this side especially: what a phone
   * calls a photo of a worksheet says nothing to the teacher who has to mark it.
   */
  function onRenameStaged(index: number, name: string) {
    setPendingFiles((prev) =>
      prev.map((file, i) => (i === index ? renameFile(file, name) : file)),
    );
  }

  async function onSubmit() {
    try {
      // Files go first, and necessarily so: a submission is its attachments, and the
      // upload endpoint is what creates the row. Uploading is what makes there be
      // something to hand in. Each file leaves the staging list as it lands, so a retry
      // after a failure part-way through does not send the same file twice.
      for (const file of pendingFiles) {
        await upload.mutateAsync({ assignmentId: assignment.id, file });
        setPendingFiles((prev) => prev.filter((staged) => staged !== file));
      }

      // Uploading leaves the submission Pending; this call is what hands it in.
      await submit.mutateAsync({
        assignmentId: assignment.id,
        submissionId: submission?.id,
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
      />

      {/* Where the summary rail used to be: where this assignment stands and the dates
          and figures behind it, on one line under the title. The same strip the teacher's
          page carries, answering the student's question instead — am I done, and by when. */}
      <MetaBar>
        {submission ? <SubmissionStatusBadge status={submission.status} /> : <NotStartedBadge />}
        <DeadlineBadge urgency={urgency}>
          {urgency === 'overdue' ? 'Closed' : 'Due'} {formatRelative(assignment.deadlineUtc)}
        </DeadlineBadge>
        <MetaDivider />
        <MetaItem icon={CalendarClock} label="Deadline">
          {formatDateTime(assignment.deadlineUtc)}
        </MetaItem>
        <MetaItem icon={isGraded ? Award : Target} label={isGraded ? 'Marks' : 'Out of'}>
          <span className="tabular-nums">
            {isGraded ? formatMarks(submission.marks, submission.marksOutOf) : assignment.maxMarks}
          </span>
        </MetaItem>
        {/* Only once there is a time to show: "Submitted —" beside a badge already
            reading "Not started" says the same thing twice, and less clearly. */}
        {submission?.submittedAtUtc && (
          <MetaItem icon={CheckCheck} label="Submitted">
            {formatDateTime(submission.submittedAtUtc)}
          </MetaItem>
        )}
        <MetaItem icon={History} label="Late changes">
          {assignment.allowResubmission ? 'Allowed' : 'Not allowed'}
        </MetaItem>
      </MetaBar>

      {/* `grid-cols-[minmax(0,1fr)]` rather than a bare single column: an `auto` track is
          sized by its item's min-content, so one unbreakable file name or URL inside would
          widen the column past the viewport and scroll the whole page sideways. Tailwind's
          `grid-cols-3` already spells its tracks `minmax(0,1fr)`, which is why only the
          small-screen layout was affected — this gives the one-column case the same floor. */}
      <div className="grid grid-cols-[minmax(0,1fr)] gap-6 lg:grid-cols-3 lg:items-start">
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

          {/* Always shown, even with nothing attached: a student who cannot find the
              materials should be told there are none, not left wondering whether the
              panel failed to load or they missed it. */}
          <SectionPanel
            title="Materials from your teacher"
            description={
              assignment.files.length > 0
                ? `${assignment.files.length} file${assignment.files.length > 1 ? 's' : ''}`
                : 'No files attached'
            }
            icon={Paperclip}
            bodyClassName="space-y-2 p-5"
          >
            {assignment.files.length > 0 ? (
              assignment.files.map((file) => (
                <FileRow
                  key={file.id}
                  name={file.originalFileName}
                  size={file.fileSizeBytes}
                  onView={
                    // Whatever the teacher attached is read here — a picture, the PDF
                    // brief, or a Word document. Only a legacy `.doc` keeps download alone.
                    canPreview(file.contentType, file.originalFileName)
                      ? () => setViewing({ file: toPreview(file), source: 'material' })
                      : undefined
                  }
                  onDownload={() => downloadAssignmentFile(file.id, file.originalFileName)}
                />
              ))
            ) : (
              <p className="text-sm text-muted-foreground">
                Your teacher did not attach any files to this assignment — everything you need
                should be in the instructions above.
              </p>
            )}
          </SectionPanel>
        </div>

        {/* The student's own side of the assignment: what they handed in, and what came
            back for it. The facts moved to the strip under the title, so the rail carries
            only these two. It keeps its own scroll — an alert, three files, a drop area
            and the mark can outgrow a short viewport, and the Submit button must never be
            the part left off-screen. */}
        <aside className="space-y-6 lg:sticky lg:top-20 lg:max-h-[calc(100vh-6rem)] lg:overflow-y-auto">
          <SectionPanel
            title="Your submission"
            description={
              readOnly ? 'Closed for changes' : `${attachmentCount} of ${MAX_FILES} attached`
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
              <div className="space-y-2">
                {files.map((file) => (
                  <FileRow
                    key={file.id}
                    name={file.originalFileName}
                    size={file.fileSizeBytes}
                    onView={
                      // Checking what was handed in should not mean downloading it back.
                      canPreview(file.contentType, file.originalFileName)
                        ? () => setViewing({ file: toPreview(file), source: 'submission' })
                        : undefined
                    }
                    onDownload={() => downloadSubmissionFile(file.id, file.originalFileName)}
                    // Already handed in, so this goes to the server — and only while the
                    // submission is still open, which is the same rule as removing it.
                    onRename={
                      readOnly
                        ? undefined
                        : (fileName) => renameFileOnServer.mutate({ fileId: file.id, fileName })
                    }
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
                    pending
                    onRename={(name) => onRenameStaged(index, name)}
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
                <FileDropzone
                  remaining={MAX_FILES - attachmentCount}
                  busy={upload.isPending}
                  disabled={isBusy}
                  onFiles={(picked) => setPendingFiles((prev) => [...prev, ...picked])}
                />
                <Button className="w-full" onClick={onSubmit} disabled={isBusy || !canHandIn}>
                  {isBusy && <Loader2 className="size-4 animate-spin" />}
                  {!handedIn ? 'Submit' : pendingFiles.length > 0 ? 'Update submission' : 'Submitted'}
                </Button>
                {pendingFiles.length > 0 && (
                  <p className="text-xs text-muted-foreground">
                    Sent when you submit — rename anything unclear first.
                  </p>
                )}
              </>
            )}
          </SectionPanel>

          {/* Directly under what was handed in, because that is what it is about: the mark
              for these files, and the teacher's words on them. */}
          {isGraded && (
            <section className="panel min-w-0 space-y-3 overflow-hidden border-success/25 bg-success-muted/40 p-5">
              <div className="flex items-center gap-2">
                <Award className="size-5 text-success" />
                <h2 className="font-heading text-sm font-semibold">
                  Marked: {formatMarks(submission.marks, submission.marksOutOf)}
                </h2>
              </div>
              {submission.feedback && (
                <p className="flex gap-2 text-sm text-muted-foreground">
                  <MessageSquareQuote className="mt-0.5 size-4 shrink-0" />
                  {/* A teacher's own words, so their line breaks are kept — but a long
                      unbroken run in them still has to wrap rather than widen the rail. */}
                  <span className="min-w-0 wrap-break-word whitespace-pre-wrap">
                    {submission.feedback}
                  </span>
                </p>
              )}
              {submission.reviewedByName && (
                <p className="text-xs text-muted-foreground">
                  by {submission.reviewedByName} · {formatDateTime(submission.reviewedAtUtc)}
                </p>
              )}
            </section>
          )}
        </aside>
      </div>

      <FilePreviewDialog
        file={viewing?.file ?? null}
        loadBlob={viewing?.source === 'submission' ? fetchSubmissionFile : fetchAssignmentFile}
        onDownload={(file) =>
          viewing?.source === 'submission'
            ? downloadSubmissionFile(file.id, file.name)
            : downloadAssignmentFile(file.id, file.name)
        }
        onClose={() => setViewing(null)}
      />
    </div>
  );
}
