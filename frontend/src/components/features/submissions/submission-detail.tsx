'use client';

import Link from 'next/link';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import {
  Award,
  ClipboardList,
  FileText,
  Inbox,
  Info,
  Loader2,
  Paperclip,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { RichText } from '@/components/ui/rich-text';
import { Textarea } from '@/components/ui/textarea';
import { DetailSkeleton, Fact, FileRow } from '@/components/shared/detail';
import { BackLink, PageHeader } from '@/components/shared/page-header';
import { SectionPanel } from '@/components/shared/section-panel';
import { EmptyState, ErrorState } from '@/components/shared/states';
import { SubmissionStatusBadge } from '@/components/shared/status-badge';
import {
  downloadSubmissionFile,
  useReviewSubmission,
  useSubmission,
} from '@/hooks/use-submissions';
import { ApiError } from '@/lib/api';
import { formatDateTime, formatMarks, formatRelative, initials } from '@/lib/format';
import { reviewSchema, type ReviewInput, type ReviewValues } from '@/schemas';
import type { Submission } from '@/types/api';

/**
 * One student's work, in full, with the marking alongside it. A page rather than a
 * dialog because marking is reading first: the answer, its attachments and the mark
 * being given all need to be visible at once, and a marked piece deserves a URL a
 * teacher can come back to.
 */
export function SubmissionDetail({
  submissionId,
  readOnly = false,
}: {
  submissionId: string;
  /** Admins browse Coursework read-only — no marking, no saving. */
  readOnly?: boolean;
}) {
  const { data: submission, isLoading, isError, error } = useSubmission(submissionId);

  if (isError) {
    const missing = error instanceof ApiError && (error.status === 404 || error.status === 403);

    return (
      <div className="space-y-6">
        <BackLink href="/submissions" label="All submissions" />
        {missing ? (
          <EmptyState
            icon={Inbox}
            className="panel"
            title="This submission is not available"
            description="It may have been withdrawn, or it belongs to an assignment you do not teach."
            action={
              <Button asChild size="sm" variant="outline">
                <Link href="/submissions">Back to submissions</Link>
              </Button>
            }
          />
        ) : (
          <ErrorState
            className="panel"
            title="Could not load this submission"
            message={error instanceof Error ? error.message : undefined}
          />
        )}
      </div>
    );
  }

  if (isLoading || !submission) return <DetailSkeleton />;

  return <Detail submission={submission} readOnly={readOnly} />;
}

function Detail({ submission, readOnly }: { submission: Submission; readOnly: boolean }) {
  const review = useReviewSubmission();

  // Set when the submission row is created, from the assignment's maximum — so the
  // bound the form validates against is the same one the API will apply.
  const maxMarks = submission.marksOutOf ?? 100;
  const isGraded = submission.status === 'Graded';

  // <what the fields hold, context, what validation produces> — `marks` is coerced,
  // so the first and last are not the same type. Seeded at mount from the saved review;
  // the component is remounted per submission, so no reset-on-change is needed.
  const form = useForm<ReviewInput, unknown, ReviewValues>({
    resolver: zodResolver(reviewSchema(maxMarks)),
    defaultValues: { marks: submission.marks ?? 0, feedback: submission.feedback ?? '' },
  });

  const errors = form.formState.errors;

  async function onSubmit(values: ReviewValues) {
    // Stays on the page afterwards: the mark just given is part of what this page is
    // for, and the invalidated query paints it back in place.
    await review.mutateAsync({
      id: submission.id,
      marks: values.marks,
      feedback: values.feedback?.trim() || undefined,
      status: 'Graded',
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/submissions', label: 'All submissions' }}
        eyebrow="Submission"
        title={submission.studentName}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <SubmissionStatusBadge status={submission.status} />
            {submission.submittedAtUtc && (
              <span className="text-xs text-muted-foreground">
                handed in {formatRelative(submission.submittedAtUtc)}
              </span>
            )}
          </div>
        }
      />

      {/* The assignment is the context for everything below, so it is a link rather
          than a line of description text. */}
      <Link
        href={`/assignments/${submission.assignmentId}`}
        className="panel-interactive flex items-center gap-3 px-5 py-3.5"
      >
        <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
          <ClipboardList className="size-4" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="eyebrow">Assignment</p>
          <p className="truncate text-sm font-medium">{submission.assignmentTitle}</p>
        </div>
        <span className="text-xs font-medium text-primary">Open</span>
      </Link>

      <div className="grid gap-6 lg:grid-cols-3 lg:items-start">
        <div className="space-y-6 lg:col-span-2">
          {submission.status === 'Late' && (
            <Alert>
              <AlertDescription>This work was submitted after the deadline.</AlertDescription>
            </Alert>
          )}

          <SectionPanel title="Answer" icon={FileText} bodyClassName="p-5">
            {submission.content ? (
              <RichText content={submission.content} />
            ) : (
              <p className="text-sm text-muted-foreground">
                No written answer — see the attachments.
              </p>
            )}
          </SectionPanel>

          {submission.files.length > 0 && (
            <SectionPanel
              title="Attachments"
              description={`${submission.files.length} file${submission.files.length > 1 ? 's' : ''}`}
              icon={Paperclip}
              bodyClassName="divide-y"
            >
              {submission.files.map((file) => (
                <FileRow
                  key={file.id}
                  name={file.originalFileName}
                  size={file.fileSizeBytes}
                  onDownload={() => downloadSubmissionFile(file.id, file.originalFileName)}
                />
              ))}
            </SectionPanel>
          )}

          <SectionPanel
            title="Marking"
            description={
              readOnly
                ? 'Read-only — only the teacher who set the work can mark it'
                : isGraded
                  ? 'Already marked — you can still change it'
                  : `Give a mark out of ${maxMarks}, and say why`
            }
            icon={Award}
            bodyClassName="space-y-4 p-5"
          >
            {readOnly ? (
              <>
                <div className="space-y-1.5">
                  <p className="eyebrow">Marks</p>
                  <p className="text-sm font-medium tabular-nums">
                    {submission.marks != null
                      ? formatMarks(submission.marks, maxMarks)
                      : 'Not marked yet'}
                  </p>
                </div>
                <div className="space-y-1.5">
                  <p className="eyebrow">Feedback</p>
                  {submission.feedback ? (
                    <p className="text-sm whitespace-pre-wrap">{submission.feedback}</p>
                  ) : (
                    <p className="text-sm text-muted-foreground">No feedback given yet.</p>
                  )}
                </div>
              </>
            ) : (
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
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
                    rows={5}
                    placeholder="What did they do well, and what should they work on?"
                    {...form.register('feedback')}
                  />
                  {errors.feedback && (
                    <p className="text-xs text-danger">{errors.feedback.message}</p>
                  )}
                </div>

                <div className="flex flex-col-reverse items-stretch gap-2 border-t pt-4 sm:flex-row sm:items-center sm:justify-end">
                  <Button asChild type="button" variant="outline">
                    <Link href="/submissions">Back to list</Link>
                  </Button>
                  <Button type="submit" disabled={review.isPending}>
                    {review.isPending && <Loader2 className="size-4 animate-spin" />}
                    {isGraded ? 'Update mark' : 'Save mark'}
                  </Button>
                </div>
              </form>
            )}
          </SectionPanel>
        </div>

        <aside className="lg:sticky lg:top-20">
          <SectionPanel title="At a glance" icon={Info} bodyClassName="divide-y">
            <Fact label="Student">
              <span className="inline-flex items-center gap-2">
                <Avatar className="size-6">
                  <AvatarFallback className="text-[10px]">
                    {initials(submission.studentName)}
                  </AvatarFallback>
                </Avatar>
                {submission.studentName}
              </span>
            </Fact>
            <Fact label="Status">
              <SubmissionStatusBadge status={submission.status} />
            </Fact>
            <Fact label="Handed in">
              <span className="block">{formatDateTime(submission.submittedAtUtc)}</span>
              {submission.submittedAtUtc && (
                <span className="block text-xs font-normal text-muted-foreground">
                  {formatRelative(submission.submittedAtUtc)}
                </span>
              )}
            </Fact>
            <Fact label={isGraded ? 'Marks' : 'Out of'}>
              <span className="tabular-nums">
                {isGraded ? formatMarks(submission.marks, maxMarks) : maxMarks}
              </span>
            </Fact>
            <Fact label="Attachments">
              <span className="tabular-nums">{submission.files.length}</span>
            </Fact>
            {submission.reviewedByName && (
              <Fact label="Marked by">
                <span className="block">{submission.reviewedByName}</span>
                <span className="block text-xs font-normal text-muted-foreground">
                  {formatDateTime(submission.reviewedAtUtc)}
                </span>
              </Fact>
            )}
          </SectionPanel>
        </aside>
      </div>
    </div>
  );
}
