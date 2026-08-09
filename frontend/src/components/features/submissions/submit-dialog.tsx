'use client';

import { useRef, useState } from 'react';
import {
  Award,
  Download,
  Loader2,
  MessageSquareQuote,
  Paperclip,
  Trash2,
  Upload,
} from 'lucide-react';
import { toast } from 'sonner';
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
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { DeadlineBadge, SubmissionStatusBadge } from '@/components/shared/status-badge';
import { downloadAssignmentFile } from '@/hooks/use-assignments';
import {
  downloadSubmissionFile,
  useDeleteSubmissionFile,
  useSubmitAssignment,
  useUploadSubmissionFile,
} from '@/hooks/use-submissions';
import { deadlineUrgency, formatBytes, formatDateTime, formatMarks } from '@/lib/format';
import type { StudentAssignment } from '@/types/api';

/** UX-only mirror of FileStorage:AllowedExtensions; the server re-checks the bytes. */
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.txt', '.png', '.jpg', '.jpeg'];
const MAX_FILES = 3;
/** UX-only mirror of FileStorage:MaxBytes — picking is deferred, so catch this early. */
const MAX_BYTES = 2 * 1024 * 1024;

export function SubmitDialog({
  assignment,
  onClose,
}: {
  assignment: StudentAssignment | null;
  onClose: () => void;
}) {
  const submission = assignment?.submission ?? null;
  const files = submission?.files ?? [];

  // Seeded once: the caller keys this dialog per assignment, so a background refetch
  // can no longer overwrite what the student is part-way through typing.
  const [content, setContent] = useState(submission?.content ?? '');
  // Picked files are held here and only sent when the student submits — selecting a
  // file is not itself an answer, so nothing reaches the server until they say so.
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const fileInput = useRef<HTMLInputElement>(null);

  const submit = useSubmitAssignment();
  const upload = useUploadSubmissionFile();
  const removeFile = useDeleteSubmissionFile();

  if (!assignment) return null;

  const urgency = deadlineUrgency(assignment.deadlineUtc);
  const isGraded = submission?.status === 'Graded';
  const readOnly = isGraded || (urgency === 'overdue' && !assignment.allowResubmission);
  const attachmentCount = files.length + pendingFiles.length;
  const hasSomething = content.trim().length > 0 || attachmentCount > 0;
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
        assignmentId: assignment!.id,
        submissionId: submission?.id,
        content: content.trim(),
      });

      // Uploads need a submission to hang off, so they follow the answer. Each file
      // leaves the staging list as it lands, so a retry after a failure part-way
      // through does not send the same file twice.
      for (const file of pendingFiles) {
        await upload.mutateAsync({ assignmentId: assignment!.id, file });
        setPendingFiles((prev) => prev.filter((staged) => staged !== file));
      }
    } catch {
      // The mutations already report the failure; stay open so nothing is lost.
      return;
    }

    onClose();
  }

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <div className="flex flex-wrap items-center gap-2">
            <DeadlineBadge urgency={urgency}>
              {urgency === 'overdue' ? 'Closed' : 'Due'} {formatDateTime(assignment.deadlineUtc)}
            </DeadlineBadge>
            {submission && <SubmissionStatusBadge status={submission.status} />}
          </div>
          <DialogTitle className="text-left">{assignment.title}</DialogTitle>
          <DialogDescription className="text-left">
            {assignment.courseName} · {assignment.teacherName} · {assignment.maxMarks} marks
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-5">
          <section className="rounded-lg border bg-muted/40 p-4">
            <h3 className="mb-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
              Instructions
            </h3>
            <p className="text-sm whitespace-pre-wrap">{assignment.description}</p>
          </section>

          {assignment.files.length > 0 && (
            <section className="space-y-2">
              <Label>Materials from your teacher</Label>
              <ul className="divide-y rounded-lg border">
                {assignment.files.map((file) => (
                  <li key={file.id} className="flex items-center gap-3 px-3 py-2">
                    <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm">{file.originalFileName}</p>
                      <p className="text-xs text-muted-foreground">{formatBytes(file.fileSizeBytes)}</p>
                    </div>
                    <Button
                      size="icon"
                      variant="ghost"
                      onClick={() => downloadAssignmentFile(file.id, file.originalFileName)}
                      aria-label={`Download ${file.originalFileName}`}
                    >
                      <Download className="size-4" />
                    </Button>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {isGraded && (
            <section className="space-y-2 rounded-lg border bg-success-muted/40 p-4">
              <div className="flex items-center gap-2">
                <Award className="size-4 text-success" />
                <h3 className="text-sm font-medium">
                  Marked: {formatMarks(submission.marks, submission.marksOutOf)}
                </h3>
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
            <Textarea
              id="answer"
              rows={6}
              value={content}
              onChange={(event) => setContent(event.target.value)}
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
              <ul className="divide-y rounded-lg border">
                {files.map((file) => (
                  <li key={file.id} className="flex items-center gap-3 px-3 py-2">
                    <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm">{file.originalFileName}</p>
                      <p className="text-xs text-muted-foreground">{formatBytes(file.fileSizeBytes)}</p>
                    </div>
                    <Button
                      size="icon"
                      variant="ghost"
                      onClick={() => downloadSubmissionFile(file.id, file.originalFileName)}
                      aria-label={`Download ${file.originalFileName}`}
                    >
                      <Download className="size-4" />
                    </Button>
                    {!readOnly && (
                      <Button
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
                {/* Staged picks: no id and nothing to download yet, so they read as
                    pending until the answer is submitted. */}
                {pendingFiles.map((file, index) => (
                  <li
                    key={`${file.name}-${index}`}
                    className="flex items-center gap-3 px-3 py-2"
                  >
                    <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm">{file.name}</p>
                      <p className="text-xs text-muted-foreground">
                        {formatBytes(file.size)}
                      </p>
                    </div>
                    <Button
                      size="icon"
                      variant="ghost"
                      disabled={isBusy}
                      onClick={() =>
                        setPendingFiles((prev) => prev.filter((_, i) => i !== index))
                      }
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
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {readOnly ? 'Close' : 'Cancel'}
          </Button>
          {!readOnly && (
            <Button onClick={onSubmit} disabled={isBusy || !hasSomething}>
              {isBusy && <Loader2 className="size-4 animate-spin" />}
              {submission && submission.status !== 'Pending' ? 'Update answer' : 'Submit answer'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
