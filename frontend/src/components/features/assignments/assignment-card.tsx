'use client';

import Link from 'next/link';
import { Award, MessageSquareQuote, Paperclip } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DeadlineBadge,
  NotStartedBadge,
  SubmissionStatusBadge,
} from '@/components/shared/status-badge';
import { deadlineUrgency, formatDateTime, formatMarks, formatRelative } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { StudentAssignment } from '@/types/api';

/** The card's top edge carries its urgency, so a grid can be triaged without reading. */
const URGENCY_STRIPE = {
  overdue: 'bg-danger',
  'due-soon': 'bg-warning',
  upcoming: 'bg-primary/30',
} as const;

/**
 * A student's view of one assignment: what it is, when it is due, and where their own
 * submission stands. Marks and feedback are shown inline once graded — that is the
 * outcome the student came for.
 */
export function AssignmentCard({
  assignment,
  onOpen,
  href,
}: {
  assignment: StudentAssignment;
  onOpen?: (assignment: StudentAssignment) => void;
  href?: string;
}) {
  const { submission } = assignment;
  const urgency = deadlineUrgency(assignment.deadlineUtc);
  const isGraded = submission?.status === 'Graded';
  const canStillEdit = !isGraded && (urgency !== 'overdue' || assignment.allowResubmission);

  const actionLabel = !submission || submission.status === 'Pending' ? 'Submit' : 'Resubmit';

  // The whole card opens the submit dialog, so it behaves like one big button. Only
  // wired up for the student flow (`onOpen`) — the link-based (`href`) card stays as-is.
  const openable = Boolean(onOpen);
  const open = () => onOpen?.(assignment);

  return (
    <article
      className={cn('panel-interactive flex h-full flex-col overflow-hidden', openable && 'cursor-pointer')}
      onClick={openable ? open : undefined}
      onKeyDown={
        openable
          ? (e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                open();
              }
            }
          : undefined
      }
      role={openable ? 'button' : undefined}
      tabIndex={openable ? 0 : undefined}
      aria-label={openable ? `${actionLabel} — ${assignment.title}` : undefined}
    >
      <div aria-hidden className={cn('h-1 w-full', URGENCY_STRIPE[urgency])} />

      <div className="flex-1 space-y-3 p-5">
        <div className="flex items-start justify-between gap-2">
          <span className="rounded-md bg-muted px-2 py-1 font-mono text-[0.7rem] font-medium text-muted-foreground">
            {assignment.courseCode}
          </span>
          {submission ? <SubmissionStatusBadge status={submission.status} /> : <NotStartedBadge />}
        </div>

        <div className="space-y-1">
          <h3 className="text-[0.975rem] leading-snug font-semibold text-balance">
            {assignment.title}
          </h3>
          <p className="text-xs text-muted-foreground">
            {assignment.courseName} · {assignment.teacherName}
          </p>
        </div>

        <p className="line-clamp-2 text-sm text-muted-foreground">{assignment.description}</p>

        {isGraded && (
          <div className="space-y-2 rounded-lg bg-success-muted/50 p-3 ring-1 ring-success/20 ring-inset">
            <div className="flex items-center gap-2">
              <Award className="size-4 text-success" />
              <span className="text-sm font-semibold tabular-nums text-success">
                {formatMarks(submission.marks, submission.marksOutOf)}
              </span>
            </div>
            {submission.feedback && (
              <p className="flex gap-1.5 text-xs text-muted-foreground">
                <MessageSquareQuote className="mt-px size-3.5 shrink-0" />
                <span className="line-clamp-2">{submission.feedback}</span>
              </p>
            )}
          </div>
        )}

        {submission && submission.files.length > 0 && (
          <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Paperclip className="size-3.5" />
            {submission.files.length} attachment{submission.files.length > 1 ? 's' : ''}
          </p>
        )}
      </div>

      <footer className="flex items-center justify-between gap-3 border-t bg-muted/25 px-5 py-3">
        <DeadlineBadge urgency={urgency}>
          {urgency === 'overdue' ? 'Closed ' : 'Due '}
          {formatRelative(assignment.deadlineUtc)}
        </DeadlineBadge>

        {onOpen ? (
          <Button
            size="sm"
            variant={canStillEdit ? 'default' : 'outline'}
            onClick={(e) => {
              e.stopPropagation();
              onOpen(assignment);
            }}
          >
            {canStillEdit ? actionLabel : 'View'}
          </Button>
        ) : (
          href && (
            <Button asChild size="sm" variant="outline">
              <Link href={href}>Open</Link>
            </Button>
          )
        )}
      </footer>

      <span className="sr-only">Deadline {formatDateTime(assignment.deadlineUtc)}</span>
    </article>
  );
}
