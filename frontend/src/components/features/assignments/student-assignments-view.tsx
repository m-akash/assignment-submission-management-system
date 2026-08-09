'use client';

import { useMemo, useState } from 'react';
import { ClipboardList, SearchX } from 'lucide-react';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
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

const PAGE_SIZE = 12;

export function StudentAssignmentsView() {
  const [search, setSearch] = useState('');
  const [courseId, setCourseId] = useState('');
  const [tab, setTab] = useState<Tab>('all');
  const [page, setPage] = useState(1);
  const [active, setActive] = useState<StudentAssignment | null>(null);

  const filters = { search, courseId };

  // One page of cards for the grid — server-sliced so a student with dozens of
  // assignments never loads them all at once.
  const {
    items,
    pagination,
    isLoading,
    isError,
    error,
  } = useStudentAssignments({ ...filters, page, pageSize: PAGE_SIZE });

  // The full set (within a generous cap) powers the status-tab counts and the course
  // dropdown. Tabs are client-side because they depend on the student's own submission,
  // which lives in a separate list — so counts must reflect every assignment, not just
  // the current page, or the badges would under-report.
  const { items: all } = useStudentAssignments(filters);

  const courseOptions = useMemo(() => {
    const seen = new Map(all.map((item) => [item.courseId, item.courseName]));
    return [...seen].map(([value, label]) => ({ value, label }));
  }, [all]);

  const counts = useMemo(() => countByTab(all), [all]);

  // The status tab is a client-side lens over the current page only — the counts above
  // stay global. If a tab has no matches on this page, the grid shows the empty state
  // rather than silently dumping the user back onto "All".
  const visible = useMemo(() => items.filter((item) => matchesTab(item, tab)), [items, tab]);

  // Keep the active card in sync after a submit so marks and attachments refresh in place.
  const activeAssignment = active ? (items.find((item) => item.id === active.id) ?? active) : null;

  /** Any filter change invalidates the current page number. */
  function withPageReset<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Coursework"
        title="Assignments"
        icon={ClipboardList}
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
            onChange={withPageReset(setSearch)}
            placeholder="Search by title…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={courseId}
            onChange={withPageReset(setCourseId)}
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
          className="panel"
        />
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {visible.map((assignment) => (
              <AssignmentCard key={assignment.id} assignment={assignment} onOpen={setActive} />
            ))}
          </div>

          {pagination && (
            <PaginationBar pagination={pagination} onPageChange={setPage} itemLabel="assignments" />
          )}
        </>
      )}

      {/* Keyed per assignment so the draft answer and any staged attachments are
          discarded when the dialog closes, rather than following the student to the
          next assignment they open. */}
      <SubmitDialog
        key={activeAssignment?.id ?? 'none'}
        assignment={activeAssignment}
        onClose={() => setActive(null)}
      />
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
