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
import { Label } from '@/components/ui/label';
import { RichText } from '@/components/ui/rich-text';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { BackLink, DetailSkeleton, Fact, FileRow } from '@/components/shared/detail';
import { PageHeader } from '@/components/shared/page-header';
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
import { deadlineUrgency, formatDateTime, formatMarks, formatRelative } from '@/lib/format';
import { isRichTextEmpty } from '@/lib/rich-text';
import type { StudentAssignment } from '@/types/api';

/** UX-only mirror of FileStorage:AllowedExtensions; the server re-checks the bytes. */
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];
const MAX_FILES = 3;
/** UX-only mirror of FileStorage:MaxBytes — picking is deferred, so catch this early. */
const MAX_BYTES = 2 * 1024 * 1024;

/**
 * One assignment, in full: the brief, the teacher's materials, and the student's own
 * answer on the same screen. A page rather than a dialog because this is the work
 * itself — it deserves the room, a URL the student can return to, and the browser's
 * own back button.
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

  // `null` means "showing the saved answer". Once the student types, their draft wins —
  // so a background refetch can never overwrite work in progress.
  const [draft, setDraft] = useState<string | null>(null);
  // Picked files are held here and only sent when the student submits — selecting a
  // file is not itself an answer, so nothing reaches the server until they say so.
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const fileInput = useRef<HTMLInputElement>(null);

  const submit = useSubmitAssignment();
  const upload = useUploadSubmissionFile();
  const removeFile = useDeleteSubmissionFile();

  const content = draft ?? submission?.content ?? '';
  // The editor reports "" for an emptied document, but a saved answer of "<p></p>" would
  // otherwise read as an answer — so what counts as written is judged on the words.
  const hasAnswer = !isRichTextEmpty(content);
  const urgency = deadlineUrgency(assignment.deadlineUtc);
  const isGraded = submission?.status === 'Graded';
  const readOnly = isGraded || (urgency === 'overdue' && !assignment.allowResubmission);
  const attachmentCount = files.length + pendingFiles.length;
  const hasSomething = hasAnswer || attachmentCount > 0;
  const isBusy = submit.isPending || upload.isPending;

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
      await submit.mutateAsync({
        assignmentId: assignment.id,
        submissionId: submission?.id,
        // Sent empty when the answer holds no words, so a file-only submission is not
        // recorded as having an answer made of empty paragraphs.
        content: hasAnswer ? content : '',
      });

      // Uploads need a submission to hang off, so they follow the answer. Each file
      // leaves the staging list as it lands, so a retry after a failure part-way
      // through does not send the same file twice.
      for (const file of pendingFiles) {
        await upload.mutateAsync({ assignmentId: assignment.id, file });
        setPendingFiles((prev) => prev.filter((staged) => staged !== file));
      }
    } catch {
      // The mutations already report the failure; keep the draft so nothing is lost.
      return;
    }

    // Saved: fall back to the server's copy again.
    setDraft(null);
  }

  return (
    <div className="space-y-6">
      <BackLink href="/assignments" label="All assignments" />

      <PageHeader
        eyebrow={`${assignment.courseCode} · ${assignment.courseName}`}
        title={assignment.title}
        icon={ClipboardList}
        description={`Set by ${assignment.teacherName} for ${assignment.className}`}
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
          <SectionPanel title="Instructions" icon={FileText} bodyClassName="p-5">
            <RichText content={assignment.description} />
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

          <SectionPanel
            title="Your submission"
            description={
              readOnly
                ? 'Closed for changes'
                : submission && submission.status !== 'Pending'
                  ? 'Already submitted — you can still make changes'
                  : 'Type your answer, attach files, then submit'
            }
            icon={PenLine}
            bodyClassName="space-y-5 p-5"
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

            <div className="space-y-2">
              <Label htmlFor="answer">Your answer</Label>
              <RichTextEditor
                id="answer"
                value={content}
                onChange={setDraft}
                disabled={readOnly}
                placeholder="Type your answer, or attach a file below."
              />
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>Attachments</Label>
                <span className="text-xs text-muted-foreground">
                  {attachmentCount} of {MAX_FILES} · max 2 MB each
                </span>
              </div>

              {attachmentCount > 0 && (
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
                      pending until the answer is submitted. */}
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
                  <p className="text-xs text-muted-foreground">
                    {pendingFiles.length > 0
                      ? 'Attached once you submit your answer.'
                      : `Allowed: ${ALLOWED_EXTENSIONS.join(', ')}`}
                  </p>
                </>
              )}
            </div>

            {!readOnly && (
              <div className="flex flex-col-reverse items-stretch gap-2 border-t pt-4 sm:flex-row sm:items-center sm:justify-end">
                <Button asChild variant="outline">
                  <Link href="/assignments">Back to list</Link>
                </Button>
                <Button onClick={onSubmit} disabled={isBusy || !hasSomething}>
                  {isBusy && <Loader2 className="size-4 animate-spin" />}
                  {submission && submission.status !== 'Pending' ? 'Update answer' : 'Submit answer'}
                </Button>
              </div>
            )}
          </SectionPanel>
        </div>

        <aside className="lg:sticky lg:top-20">
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
            <Fact label="Class">{assignment.className}</Fact>
            <Fact label="Teacher">{assignment.teacherName}</Fact>
          </SectionPanel>
        </aside>
      </div>
    </div>
  );
}
