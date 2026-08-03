'use client';

import Link from 'next/link';
import { Award, CheckCircle2, ClipboardList, Clock, TimerOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/shared/page-header';
import { StatCard } from '@/components/shared/stat-card';
import { CardGridSkeleton, EmptyState, ErrorState } from '@/components/shared/states';
import { useStudentAssignments } from '@/hooks/use-submissions';
import { deadlineUrgency, formatMarks } from '@/lib/format';
import { AssignmentCard } from '@/components/features/assignments/assignment-card';

export function StudentOverview({ name, className }: { name: string; className: string | null }) {
  const { items, isLoading, isError, error } = useStudentAssignments({});
  const firstName = name.split(' ')[0];

  const outstanding = items.filter(
    (item) => !item.submission || item.submission.status === 'Pending',
  );
  const graded = items.filter((item) => item.submission?.status === 'Graded');
  const overdue = outstanding.filter((item) => deadlineUrgency(item.deadlineUtc) === 'overdue');

  // Average shown as a percentage so assignments with different maximums are comparable.
  const averagePercent =
    graded.length > 0
      ? Math.round(
          (graded.reduce(
            (sum, item) => sum + (item.submission!.marks ?? 0) / (item.submission!.marksOutOf || 1),
            0,
          ) /
            graded.length) *
            100,
        )
      : null;

  // Soonest deadline first — that is the thing a student needs next.
  const upNext = [...outstanding]
    .sort((a, b) => a.deadlineUtc.localeCompare(b.deadlineUtc))
    .slice(0, 3);

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Hello, ${firstName}`}
        description={className ? `Assignments for ${className}.` : 'Your assignments.'}
        actions={
          <Button asChild variant="outline">
            <Link href="/assignments">All assignments</Link>
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="To do"
          value={outstanding.length}
          hint="Not submitted yet"
          icon={ClipboardList}
          tone="warning"
          loading={isLoading}
        />
        <StatCard
          label="Overdue"
          value={overdue.length}
          hint={overdue.length > 0 ? 'Submit as soon as you can' : 'Nothing past its deadline'}
          icon={TimerOff}
          tone={overdue.length > 0 ? 'danger' : 'neutral'}
          loading={isLoading}
        />
        <StatCard
          label="Graded"
          value={graded.length}
          icon={CheckCircle2}
          tone="success"
          loading={isLoading}
        />
        <StatCard
          label="Average"
          value={averagePercent === null ? '—' : `${averagePercent}%`}
          hint={graded.length > 0 ? `Across ${graded.length} graded` : 'No marks yet'}
          icon={Award}
          tone="primary"
          loading={isLoading}
        />
      </div>

      <section className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="font-medium">Up next</h2>
          {items.length > upNext.length && (
            <Button asChild variant="ghost" size="sm">
              <Link href="/assignments">See all {items.length}</Link>
            </Button>
          )}
        </div>

        {isError ? (
          <ErrorState message={error instanceof Error ? error.message : undefined} />
        ) : isLoading ? (
          <CardGridSkeleton count={3} />
        ) : upNext.length === 0 ? (
          <EmptyState
            icon={CheckCircle2}
            title="You are all caught up"
            description="Every assignment for your class has been submitted."
            className="rounded-xl border bg-card"
          />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {upNext.map((assignment) => (
              <AssignmentCard key={assignment.id} assignment={assignment} href="/assignments" />
            ))}
          </div>
        )}
      </section>

      {graded.length > 0 && (
        <section className="rounded-xl border bg-card">
          <header className="border-b px-5 py-4">
            <h2 className="font-medium">Recent marks</h2>
          </header>
          <ul className="divide-y">
            {graded.slice(0, 5).map((item) => (
              <li key={item.id} className="flex items-center gap-4 px-5 py-3">
                <Clock className="size-4 shrink-0 text-muted-foreground" />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{item.title}</p>
                  <p className="truncate text-xs text-muted-foreground">{item.subjectName}</p>
                </div>
                <span className="text-sm font-medium tabular-nums">
                  {formatMarks(item.submission!.marks, item.submission!.marksOutOf)}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
