'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Download, Loader2, Paperclip } from 'lucide-react';
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
import { RichText } from '@/components/ui/rich-text';
import { Textarea } from '@/components/ui/textarea';
import { SubmissionStatusBadge } from '@/components/shared/status-badge';
import { downloadSubmissionFile, useReviewSubmission } from '@/hooks/use-submissions';
import { formatBytes, formatDateTime, formatMarks } from '@/lib/format';
import { reviewSchema, type ReviewInput, type ReviewValues } from '@/schemas';
import type { Submission } from '@/types/api';

export function ReviewDialog({
  submission,
  maxMarks,
  readOnly = false,
  onClose,
}: {
  submission: Submission | null;
  /** From the assignment; the schema bounds marks by it, as the API does. */
  maxMarks: number;
  /** Admins browse Coursework read-only — no marking, no saving. */
  readOnly?: boolean;
  onClose: () => void;
}) {
  const review = useReviewSubmission();

  // <what the fields hold, context, what validation produces> — `marks` is coerced,
  // so the first and last are not the same type.
  const form = useForm<ReviewInput, unknown, ReviewValues>({
    resolver: zodResolver(reviewSchema(maxMarks)),
    defaultValues: { marks: 0, feedback: '' },
  });

  useEffect(() => {
    if (submission) {
      form.reset({ marks: submission.marks ?? 0, feedback: submission.feedback ?? '' });
    }
  }, [submission, form]);

  if (!submission) return null;

  async function onSubmit(values: ReviewValues) {
    await review.mutateAsync({
      id: submission!.id,
      marks: values.marks,
      feedback: values.feedback?.trim() || undefined,
      status: 'Graded',
    });
    onClose();
  }

  const errors = form.formState.errors;

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <SubmissionStatusBadge status={submission.status} />
            {submission.submittedAtUtc && (
              <span className="text-xs text-muted-foreground">
                submitted {formatDateTime(submission.submittedAtUtc)}
              </span>
            )}
          </div>
          <DialogTitle className="text-left">{submission.studentName}</DialogTitle>
          <DialogDescription className="text-left">{submission.assignmentTitle}</DialogDescription>
        </DialogHeader>

        <div className="space-y-5">
          {submission.status === 'Late' && (
            <Alert>
              <AlertDescription>This work was submitted after the deadline.</AlertDescription>
            </Alert>
          )}

          <section>
            <h3 className="mb-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
              Answer
            </h3>
            {submission.content ? (
              <RichText
                content={submission.content}
                className="rounded-lg border bg-muted/40 p-4"
              />
            ) : (
              <p className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
                No written answer — see the attachments.
              </p>
            )}
          </section>

          {submission.files.length > 0 && (
            <section className="space-y-2">
              <h3 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
                Attachments
              </h3>
              <ul className="divide-y rounded-lg border">
                {submission.files.map((file) => (
                  <li key={file.id} className="flex items-center gap-3 px-3 py-2">
                    <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm">{file.originalFileName}</p>
                      <p className="text-xs text-muted-foreground">{formatBytes(file.fileSizeBytes)}</p>
                    </div>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => downloadSubmissionFile(file.id, file.originalFileName)}
                    >
                      <Download className="size-4" />
                      Download
                    </Button>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {readOnly ? (
            <>
              <section>
                <h3 className="mb-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
                  Marks
                </h3>
                <p className="text-sm">
                  {submission.marks != null
                    ? `${formatMarks(submission.marks, maxMarks)}`
                    : 'Not graded yet'}
                </p>
              </section>
              <section>
                <h3 className="mb-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase">
                  Feedback
                </h3>
                {submission.feedback ? (
                  <p className="rounded-lg border bg-muted/40 p-4 text-sm whitespace-pre-wrap">
                    {submission.feedback}
                  </p>
                ) : (
                  <p className="text-sm text-muted-foreground">No feedback given yet.</p>
                )}
              </section>
            </>
          ) : (
            <form id="review-form" onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
              <div className="space-y-2">
                <Label htmlFor="marks">Marks (out of {maxMarks})</Label>
                <Input
                  id="marks"
                  type="number"
                  min={0}
                  max={maxMarks}
                  step="0.5"
                  className="max-w-35"
                  {...form.register('marks')}
                />
                {errors.marks && <p className="text-xs text-danger">{errors.marks.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="feedback">Feedback</Label>
                <Textarea
                  id="feedback"
                  rows={4}
                  placeholder="What did they do well, and what should they work on?"
                  {...form.register('feedback')}
                />
                {errors.feedback && <p className="text-xs text-danger">{errors.feedback.message}</p>}
              </div>
            </form>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {readOnly ? 'Close' : 'Cancel'}
          </Button>
          {!readOnly && (
            <Button type="submit" form="review-form" disabled={review.isPending}>
              {review.isPending && <Loader2 className="size-4 animate-spin" />}
              {submission.status === 'Graded' ? 'Update mark' : 'Save mark'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
