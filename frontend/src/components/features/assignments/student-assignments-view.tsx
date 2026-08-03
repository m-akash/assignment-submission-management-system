'use client';

import { useMemo, useState } from 'react';
import { ClipboardList, SearchX } from 'lucide-react';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { SearchInput } from '@/components/shared/search-input';
import { CardGridSkeleton, EmptyState, ErrorState } from '@/components/shared/states';
import { useStudentAssignments } from '@/hooks/use-submissions';
import { deadlineUrgency } from '@/lib/format';
import { AssignmentCard } from './assignment-card';
import { SubmitDialog } from '@/components/features/submissions/submit-dialog';
import type { StudentAssignment } from '@/types/api';

type Tab = 'all' | 'todo' | 'submitted' | 'graded' | 'overdue';

const TABS: { value: Tab; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'todo', label: 'To do' },
  { value: 'submitted', label: 'Submitted' },
  { value: 'graded', label: 'Graded' },
  { value: 'overdue', label: 'Overdue' },
];

export function StudentAssignmentsView() {
  const [search, setSearch] = useState('');
  const [courseId, setCourseId] = useState('');
  const [tab, setTab] = useState<Tab>('all');
  const [active, setActive] = useState<StudentAssignment | null>(null);

  // Title search runs server-side; the status tabs are client-side because they depend
  // on the student's own submission, which lives in a separate list.
  const { items, isLoading, isError, error } = useStudentAssignments({ search, courseId });

  const courseOptions = useMemo(() => {
    const seen = new Map(items.map((item) => [item.courseId, item.courseName]));
    return [...seen].map(([value, label]) => ({ value, label }));
  }, [items]);

  const counts = useMemo(() => countByTab(items), [items]);
  const visible = useMemo(() => items.filter((item) => matchesTab(item, tab)), [items, tab]);

  // Keep the active card in sync after a submit so marks and attachments refresh in place.
  const activeAssignment = active ? (items.find((item) => item.id === active.id) ?? active) : null;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Assignments"
        description="Everything set for your class. Submit before the deadline to avoid a late mark."
      />

      <div className="space-y-4">
        <Tabs value={tab} onValueChange={(value) => setTab(value as Tab)}>
          <TabsList>
            {TABS.map(({ value, label }) => (
              <TabsTrigger key={value} value={value} className="gap-1.5">
                {label}
                <span className="text-xs text-muted-foreground tabular-nums">{counts[value]}</span>
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        <div className="flex flex-col gap-2 sm:flex-row">
          <SearchInput
            value={search}
            onChange={setSearch}
            placeholder="Search by title…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={courseId}
            onChange={setCourseId}
            options={courseOptions}
            allLabel="All courses"
          />
        </div>
      </div>

      {isError ? (
        <ErrorState message={error instanceof Error ? error.message : undefined} />
      ) : isLoading ? (
        <CardGridSkeleton />
      ) : visible.length === 0 ? (
        <EmptyState
          icon={items.length === 0 ? ClipboardList : SearchX}
          title={items.length === 0 ? 'No assignments yet' : 'Nothing matches those filters'}
          description={
            items.length === 0
              ? 'When your teacher publishes work for your class, it will appear here.'
              : 'Try a different search term, course, or tab.'
          }
          className="rounded-xl border bg-card"
        />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {visible.map((assignment) => (
            <AssignmentCard key={assignment.id} assignment={assignment} onOpen={setActive} />
          ))}
        </div>
      )}

      <SubmitDialog assignment={activeAssignment} onClose={() => setActive(null)} />
    </div>
  );
}

function matchesTab(item: StudentAssignment, tab: Tab): boolean {
  const status = item.submission?.status;
  switch (tab) {
    case 'todo':
      return !item.submission || status === 'Pending';
    case 'submitted':
      return status === 'Submitted' || status === 'Late';
    case 'graded':
      return status === 'Graded';
    case 'overdue':
      return (
        (!item.submission || status === 'Pending') && deadlineUrgency(item.deadlineUtc) === 'overdue'
      );
    default:
      return true;
  }
}

function countByTab(items: StudentAssignment[]): Record<Tab, number> {
  return TABS.reduce(
    (counts, { value }) => ({ ...counts, [value]: items.filter((item) => matchesTab(item, value)).length }),
    {} as Record<Tab, number>,
  );
}
