'use client';

import Link from 'next/link';
import { ClipboardList, FileEdit, Inbox, Plus, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/shared/page-header';
import { StatCard } from '@/components/shared/stat-card';
import { EmptyState } from '@/components/shared/states';
import { SubmissionStatusBadge } from '@/components/shared/status-badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useAssignments } from '@/hooks/use-assignments';
import { useSubmissions } from '@/hooks/use-submissions';
import { formatRelative } from '@/lib/format';

const COUNT_ONLY = { page: 1, pageSize: 1 };

export function TeacherOverview({ name }: { name: string }) {
  const drafts = useAssignments({ ...COUNT_ONLY, status: 'Draft' });
  const published = useAssignments({ ...COUNT_ONLY, status: 'Published' });
  const awaiting = useSubmissions({ ...COUNT_ONLY, status: 'Submitted' });
  const graded = useSubmissions({ ...COUNT_ONLY, status: 'Graded' });
  const recent = useSubmissions({ page: 1, pageSize: 6, status: 'Submitted' });

  const firstName = name.split(' ')[0];

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Welcome back, ${firstName}`}
        description="Your assignments and the work waiting to be marked."
        actions={
          <Button asChild>
            <Link href="/assignments">
              <Plus className="size-4" />
              New assignment
            </Link>
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Awaiting marking"
          value={awaiting.data?.pagination.total ?? 0}
          hint="Submitted, not yet graded"
          icon={Inbox}
          tone="warning"
          loading={awaiting.isLoading}
        />
        <StatCard
          label="Graded"
          value={graded.data?.pagination.total ?? 0}
          icon={ClipboardList}
          tone="success"
          loading={graded.isLoading}
        />
        <StatCard
          label="Published"
          value={published.data?.pagination.total ?? 0}
          hint="Visible to students"
          icon={Send}
          tone="info"
          loading={published.isLoading}
        />
        <StatCard
          label="Drafts"
          value={drafts.data?.pagination.total ?? 0}
          hint="Not yet visible"
          icon={FileEdit}
          loading={drafts.isLoading}
        />
      </div>

      <section className="rounded-xl border bg-card">
        <header className="flex items-center justify-between border-b px-5 py-4">
          <div>
            <h2 className="font-medium">Waiting on you</h2>
            <p className="text-sm text-muted-foreground">The most recent submissions to mark.</p>
          </div>
          <Button asChild variant="outline" size="sm">
            <Link href="/submissions">View all</Link>
          </Button>
        </header>

        {recent.isLoading ? (
          <div className="space-y-3 p-5">
            {Array.from({ length: 3 }).map((_, index) => (
              <Skeleton key={index} className="h-12 w-full" />
            ))}
          </div>
        ) : recent.data && recent.data.items.length > 0 ? (
          <ul className="divide-y">
            {recent.data.items.map((submission) => (
              <li key={submission.id} className="flex items-center gap-4 px-5 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{submission.studentName}</p>
                  <p className="truncate text-xs text-muted-foreground">{submission.assignmentTitle}</p>
                </div>
                <span className="hidden text-xs text-muted-foreground sm:inline">
                  {formatRelative(submission.submittedAtUtc)}
                </span>
                <SubmissionStatusBadge status={submission.status} />
                <Button asChild size="sm" variant="outline">
                  <Link href={`/submissions?assignmentId=${submission.assignmentId}`}>Mark</Link>
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
      </section>
    </div>
  );
}
