'use client';

import { useEffect, useRef, useState } from 'react';
import {
  Award,
  Download,
  Loader2,
  MessageSquareQuote,
  Paperclip,
  Trash2,
  Upload,
} from 'lucide-react';
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

export function SubmitDialog({
  assignment,
  onClose,
}: {
  assignment: StudentAssignment | null;
  onClose: () => void;
}) {
  const [content, setContent] = useState('');
  const fileInput = useRef<HTMLInputElement>(null);

  const submit = useSubmitAssignment();
  const upload = useUploadSubmissionFile();
  const removeFile = useDeleteSubmissionFile();

  const submission = assignment?.submission ?? null;
  const files = submission?.files ?? [];

  useEffect(() => {
    setContent(submission?.content ?? '');
  }, [submission]);

  if (!assignment) return null;

  const urgency = deadlineUrgency(assignment.deadlineUtc);
  const isGraded = submission?.status === 'Graded';
  const readOnly = isGraded || (urgency === 'overdue' && !assignment.allowResubmission);
  const hasSomething = content.trim().length > 0 || files.length > 0;

  async function onFilePicked(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    await upload.mutateAsync({ assignmentId: assignment!.id, file });
  }

  async function onSubmit() {
    await submit.mutateAsync({
      assignmentId: assignment!.id,
      submissionId: submission?.id,
      content: content.trim(),
    });
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
                  disabled={upload.isPending || files.length >= MAX_FILES}
                  onClick={() => fileInput.current?.click()}
                >
                  {upload.isPending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Upload className="size-4" />
                  )}
                  {files.length >= MAX_FILES ? 'Attachment limit reached' : 'Attach a file'}
                </Button>
                <p className="text-xs text-muted-foreground">
                  Allowed: {ALLOWED_EXTENSIONS.join(', ')}
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
            <Button onClick={onSubmit} disabled={submit.isPending || !hasSomething}>
              {submit.isPending && <Loader2 className="size-4 animate-spin" />}
              {submission && submission.status !== 'Pending' ? 'Update answer' : 'Submit answer'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
