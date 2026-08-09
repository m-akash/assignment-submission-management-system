'use client';

import Link from 'next/link';
import { ClipboardList, FileEdit, GraduationCap, Inbox, Plus, Send } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { HeroBanner, HeroStat, heroButton } from '@/components/shared/hero-banner';
import { SectionPanel } from '@/components/shared/section-panel';
import { StatCard } from '@/components/shared/stat-card';
import { EmptyState } from '@/components/shared/states';
import { SubmissionStatusBadge } from '@/components/shared/status-badge';
import { useAssignments } from '@/hooks/use-assignments';
import { useMyTeacherMappings } from '@/hooks/use-admin-resources';
import { useSubmissions } from '@/hooks/use-submissions';
import { classLabel, formatRelative, initials } from '@/lib/format';

const COUNT_ONLY = { page: 1, pageSize: 1 };

export function TeacherOverview({ name }: { name: string }) {
  const drafts = useAssignments({ ...COUNT_ONLY, status: 'Draft' });
  const published = useAssignments({ ...COUNT_ONLY, status: 'Published' });
  const awaiting = useSubmissions({ ...COUNT_ONLY, status: 'Submitted' });
  const graded = useSubmissions({ ...COUNT_ONLY, status: 'Graded' });
  const recent = useSubmissions({ page: 1, pageSize: 6, status: 'Submitted' });
  const mappings = useMyTeacherMappings();

  const firstName = name.split(' ')[0];
  const awaitingCount = awaiting.data?.pagination.total ?? 0;
  const gradedCount = graded.data?.pagination.total ?? 0;
  const marked = awaitingCount + gradedCount;
  // How much of the arrived work is dealt with — the number a teacher actually tracks.
  const markedPercent = marked > 0 ? Math.round((gradedCount / marked) * 100) : 0;

  return (
    <div className="space-y-6">
      <HeroBanner
        eyebrow="Teaching"
        title={`Welcome back, ${firstName}`}
        description={
          awaitingCount > 0
            ? `${awaitingCount} submission${awaitingCount === 1 ? '' : 's'} ${awaitingCount === 1 ? 'is' : 'are'} waiting to be marked.`
            : 'Nothing is waiting to be marked. Set new work whenever you are ready.'
        }
        actions={
          <>
            <Button asChild size="lg" className={heroButton.solid}>
              <Link href="/assignments">
                <Plus className="size-4" />
                New assignment
              </Link>
            </Button>
            <Button asChild size="lg" variant="outline" className={heroButton.quiet}>
              <Link href="/submissions">Review submissions</Link>
            </Button>
          </>
        }
        aside={
          <div className="flex gap-3">
            <HeroStat value={awaitingCount} label="To mark" />
            <HeroStat value={published.data?.pagination.total ?? 0} label="Published" />
            <HeroStat value={mappings.data?.length ?? 0} label="Courses" />
          </div>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Awaiting marking"
          value={awaitingCount}
          hint="Submitted, not yet graded"
          icon={Inbox}
          tone="warning"
          loading={awaiting.isLoading}
          href="/submissions?status=Submitted"
        />
        <StatCard
          label="Graded"
          value={gradedCount}
          hint={marked > 0 ? `${markedPercent}% of work received` : 'No submissions yet'}
          icon={ClipboardList}
          tone="success"
          loading={graded.isLoading}
          progress={markedPercent}
          href="/submissions?status=Graded"
        />
        <StatCard
          label="Published"
          value={published.data?.pagination.total ?? 0}
          hint="Visible to students"
          icon={Send}
          tone="info"
          loading={published.isLoading}
          href="/assignments"
        />
        <StatCard
          label="Drafts"
          value={drafts.data?.pagination.total ?? 0}
          hint="Not yet visible"
          icon={FileEdit}
          loading={drafts.isLoading}
          href="/assignments"
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <SectionPanel
          title="Waiting on you"
          description="The most recent submissions to mark."
          icon={Inbox}
          action={
            <Button asChild variant="outline" size="sm">
              <Link href="/submissions">View all</Link>
            </Button>
          }
        >
          {recent.isLoading ? (
            <div className="space-y-3 p-5">
              {Array.from({ length: 3 }).map((_, index) => (
                <Skeleton key={index} className="h-12 w-full" />
              ))}
            </div>
          ) : recent.data && recent.data.items.length > 0 ? (
            <ul className="divide-y">
              {recent.data.items.map((submission) => (
                <li
                  key={submission.id}
                  className="flex items-center gap-3 px-5 py-3 transition-colors hover:bg-muted/40"
                >
                  <Avatar className="size-8 shrink-0">
                    <AvatarFallback className="bg-muted text-[11px] font-semibold">
                      {initials(submission.studentName)}
                    </AvatarFallback>
                  </Avatar>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{submission.studentName}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {submission.assignmentTitle}
                    </p>
                  </div>
                  <span className="hidden text-xs whitespace-nowrap text-muted-foreground sm:inline">
                    {formatRelative(submission.submittedAtUtc)}
                  </span>
                  <SubmissionStatusBadge status={submission.status} />
                  <Button asChild size="sm" variant="outline">
                    <Link href={`/submissions/${submission.id}`}>
                      {submission.status === 'Graded' ? 'View' : 'Mark'}
                    </Link>
                  </Button>
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              icon={Inbox}
              title="Nothing to mark"
              description="When students submit work for your assignments, it will appear here."
            />
          )}
        </SectionPanel>

        <SectionPanel
          title="Your courses"
          description="Offerings you are assigned to teach."
          icon={GraduationCap}
          action={
            <Button asChild variant="ghost" size="sm">
              <Link href="/my-courses">All</Link>
            </Button>
          }
        >
          {mappings.isLoading ? (
            <div className="space-y-3 p-5">
              {Array.from({ length: 3 }).map((_, index) => (
                <Skeleton key={index} className="h-10 w-full" />
              ))}
            </div>
          ) : mappings.data && mappings.data.length > 0 ? (
            <ul className="divide-y">
              {mappings.data.slice(0, 5).map((mapping) => (
                <li key={mapping.id}>
                  <Link
                    href={`/submissions?courseId=${mapping.courseId}&classId=${mapping.classId}`}
                    className="flex items-center gap-3 px-5 py-3 transition-colors hover:bg-muted/40"
                  >
                    <span className="shrink-0 rounded-md bg-muted px-2 py-1 font-mono text-[0.7rem] font-medium text-muted-foreground">
                      {mapping.courseCode}
                    </span>
                    <div className="min-w-0 flex-1 truncate text-sm font-medium">
                      {mapping.courseName}
                    </div>
                    <div className="min-w-0 flex-1 truncate text-sm text-muted-foreground">
                      {classLabel(mapping.classLevel, mapping.classSection)}
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              icon={GraduationCap}
              title="No courses yet"
              description="An admin assigns you to a course and class before you can set work."
            />
          )}
        </SectionPanel>
      </div>
    </div>
  );
}
