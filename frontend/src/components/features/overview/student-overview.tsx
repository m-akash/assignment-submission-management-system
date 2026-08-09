'use client';

import Link from 'next/link';
import { Award, CheckCircle2, ClipboardList, Send, TimerOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { HeroBanner, heroButton } from '@/components/shared/hero-banner';
import { ProgressRing } from '@/components/shared/progress-ring';
import { SectionPanel } from '@/components/shared/section-panel';
import { StatCard } from '@/components/shared/stat-card';
import { CardGridSkeleton, EmptyState, ErrorState } from '@/components/shared/states';
import { useStudentAssignments } from '@/hooks/use-submissions';
import { classLabel, deadlineUrgency, formatMarks, formatRelative } from '@/lib/format';
import { AssignmentCard } from '@/components/features/assignments/assignment-card';
import type { EnrolledClass } from '@/types/api';

export function StudentOverview({
  name,
  classes,
}: {
  name: string;
  /** Every class the student is enrolled in — normally one, but the model allows more. */
  classes: EnrolledClass[];
}) {
  // One label for the header, whether they sit in one class or several. The session is
  // named alongside each: a student who has moved up a grade keeps last year's enrollment,
  // so the class alone would not say which of the two is this year's.
  const className =
    classes.length > 0
      ? classes
          .map((c) => `${classLabel(c.classLevel, c.classSection)} (${c.academicYearName})`)
          .join(' · ')
      : null;

  const { items, isLoading, isError, error } = useStudentAssignments({});
  const firstName = name.split(' ')[0];

  const outstanding = items.filter(
    (item) => !item.submission || item.submission.status === 'Pending',
  );
  const submitted = items.filter(
    (item) => item.submission?.status === 'Submitted' || item.submission?.status === 'Late',
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
    .slice(0, 4);

  const donePercent = items.length > 0 ? Math.round((graded.length / items.length) * 100) : 0;

  return (
    <div className="space-y-6">
      <HeroBanner
        eyebrow={className ?? 'Your coursework'}
        title={`Hello, ${firstName}`}
        description={
          overdue.length > 0
            ? `${overdue.length} assignment${overdue.length === 1 ? '' : 's'} past the deadline. Submit as soon as you can — late work is still marked.`
            : outstanding.length > 0
              ? `${outstanding.length} assignment${outstanding.length === 1 ? '' : 's'} left to submit. Nothing is overdue.`
              : 'Everything set for your class has been submitted. Nice work.'
        }
        actions={
          <>
            <Button asChild size="lg" className={heroButton.solid}>
              <Link href="/assignments">
                <ClipboardList className="size-4" />
                All assignments
              </Link>
            </Button>
            {outstanding.length > 0 && (
              <Button asChild size="lg" variant="outline" className={heroButton.quiet}>
                <Link href="/assignments">Submit next</Link>
              </Button>
            )}
          </>
        }
        aside={<ProgressRing value={averagePercent} caption="Average" />}
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
          label="Awaiting marks"
          value={submitted.length}
          hint="Handed in, not graded yet"
          icon={Send}
          tone="info"
          loading={isLoading}
        />
        <StatCard
          label="Graded"
          value={graded.length}
          hint={items.length > 0 ? `${donePercent}% of your coursework` : 'No assignments yet'}
          icon={CheckCircle2}
          tone="success"
          loading={isLoading}
          progress={donePercent}
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.65fr)_minmax(0,1fr)]">
        <section className="space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="font-heading text-base font-semibold">Up next</h2>
              <p className="text-sm text-muted-foreground">Soonest deadline first.</p>
            </div>
            {items.length > upNext.length && (
              <Button asChild variant="outline" size="sm">
                <Link href="/assignments">See all {items.length}</Link>
              </Button>
            )}
          </div>

          {isError ? (
            <ErrorState className="panel" message={error instanceof Error ? error.message : undefined} />
          ) : isLoading ? (
            <CardGridSkeleton count={2} />
          ) : upNext.length === 0 ? (
            <EmptyState
              icon={CheckCircle2}
              title="You are all caught up"
              description="Every assignment for your class has been submitted."
              className="panel"
            />
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              {upNext.map((assignment, index) => (
                <div
                  key={assignment.id}
                  className="animate-rise"
                  style={{ '--rise-delay': `${index * 60}ms` } as React.CSSProperties}
                >
                  <AssignmentCard assignment={assignment} href={`/assignments/${assignment.id}`} />
                </div>
              ))}
            </div>
          )}
        </section>

        <SectionPanel
          title="Recent marks"
          description="Your latest graded work."
          icon={Award}
          action={
            graded.length > 0 ? (
              <Button asChild variant="ghost" size="sm">
                <Link href="/assignments">All</Link>
              </Button>
            ) : undefined
          }
        >
          {graded.length > 0 ? (
            <ul className="divide-y">
              {graded.slice(0, 6).map((item) => {
                const percent = Math.round(
                  ((item.submission!.marks ?? 0) / (item.submission!.marksOutOf || 1)) * 100,
                );
                return (
                  <li key={item.id} className="flex items-center gap-3 px-5 py-3">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{item.title}</p>
                      <p className="truncate text-xs text-muted-foreground">
                        {item.courseName} · {formatRelative(item.submission!.reviewedAtUtc)}
                      </p>
                    </div>
                    <div className="shrink-0 text-right">
                      <p className="text-sm font-semibold tabular-nums">
                        {formatMarks(item.submission!.marks, item.submission!.marksOutOf)}
                      </p>
                      {/* A share bar rather than a second number: the percentage is what
                          makes two assignments with different maximums comparable. */}
                      <div className="mt-1 h-1 w-16 overflow-hidden rounded-full bg-muted">
                        <div
                          className="h-full rounded-full bg-success"
                          style={{ width: `${Math.min(100, Math.max(0, percent))}%` }}
                        />
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          ) : (
            <EmptyState
              icon={Award}
              title="No marks yet"
              description="Marks and feedback appear here once your teacher has graded your work."
            />
          )}
        </SectionPanel>
      </div>
    </div>
  );
}
