'use client';

import Link from 'next/link';
import { Award, MessageSquareQuote, Paperclip } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DeadlineBadge,
  NotStartedBadge,
  SubmissionStatusBadge,
} from '@/components/shared/status-badge';
import { deadlineUrgency, formatDateTime, formatMarks, formatRelative } from '@/lib/format';
import type { StudentAssignment } from '@/types/api';

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

  return (
    <article className="flex flex-col rounded-xl border bg-card transition-colors hover:border-primary/40">
      <div className="flex-1 space-y-3 p-5">
        <div className="flex items-start justify-between gap-2">
          <Badge variant="secondary" className="font-mono text-[11px]">
            {assignment.subjectCode}
          </Badge>
          {submission ? <SubmissionStatusBadge status={submission.status} /> : <NotStartedBadge />}
        </div>

        <div className="space-y-1">
          <h3 className="leading-snug font-medium text-balance">{assignment.title}</h3>
          <p className="text-xs text-muted-foreground">
            {assignment.subjectName} · {assignment.teacherName}
          </p>
        </div>

        <p className="line-clamp-2 text-sm text-muted-foreground">{assignment.description}</p>

        {isGraded && (
          <div className="space-y-2 rounded-lg border bg-success-muted/40 p-3">
            <div className="flex items-center gap-2">
              <Award className="size-4 text-success" />
              <span className="text-sm font-medium tabular-nums">
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

      <footer className="flex items-center justify-between gap-3 border-t px-5 py-3">
        <DeadlineBadge urgency={urgency}>
          {urgency === 'overdue' ? 'Closed ' : 'Due '}
          {formatRelative(assignment.deadlineUtc)}
        </DeadlineBadge>

        {onOpen ? (
          <Button size="sm" variant={canStillEdit ? 'default' : 'outline'} onClick={() => onOpen(assignment)}>
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
