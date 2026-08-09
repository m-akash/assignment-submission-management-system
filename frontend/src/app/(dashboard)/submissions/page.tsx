'use client';

import { Suspense, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { ChevronRight, Inbox } from 'lucide-react';
import { useAuth } from '@/context/AuthContext';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { SubmissionStatusBadge } from '@/components/shared/status-badge';
import { useSubmissions } from '@/hooks/use-submissions';
import { formatMarks, formatRelative, initials } from '@/lib/format';
import type { SubmissionStatus } from '@/types/api';

const STATUS_OPTIONS = [
  { value: 'Submitted', label: 'Submitted' },
  { value: 'Late', label: 'Late' },
  { value: 'Graded', label: 'Graded' },
];

export default function SubmissionsPage() {
  return (
    <RoleGuard allow={['Admin', 'Teacher']}>
      <Suspense>
        <SubmissionsView />
      </Suspense>
    </RoleGuard>
  );
}

function SubmissionsView() {
  const { user } = useAuth();
  const readOnly = user?.role === 'Admin';
  const router = useRouter();

  const searchParams = useSearchParams();
  const assignmentId = searchParams.get('assignmentId') ?? undefined;

  const [search, setSearch] = useState('');
  // Seeded from the URL so the dashboard tiles can deep-link into a filtered view
  // ("Awaiting marking" → ?status=Submitted). Only the initial value comes from the
  // query string; after that the dropdown owns it, and an unknown value is ignored.
  const [status, setStatus] = useState<SubmissionStatus | ''>(() => {
    const requested = searchParams.get('status');
    return STATUS_OPTIONS.some((option) => option.value === requested)
      ? (requested as SubmissionStatus)
      : '';
  });
  const [page, setPage] = useState(1);

  const query = useSubmissions({ search, status, assignmentId, page, pageSize: 10 });
  const items = query.data?.items ?? [];
  const isFiltered = !!search || !!status;

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
        title="Submissions"
        icon={Inbox}
        description={
          assignmentId
            ? 'Filtered to one assignment. Clear the filter to see everything.'
            : 'Every submission for the assignments you teach.'
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search student or assignment…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={status}
            onChange={withPageReset((value: string) => setStatus(value as SubmissionStatus | ''))}
            options={STATUS_OPTIONS}
            allLabel="Any status"
            className="w-[160px]"
          />
        </div>

        {query.isError ? (
          <ErrorState message={query.error instanceof Error ? query.error.message : undefined} />
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Student</TableHead>
                    <TableHead>Assignment</TableHead>
                    <TableHead>Submitted</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Marks</TableHead>
                    <TableHead className="w-24">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={6} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="p-0">
                        <EmptyState
                          icon={Inbox}
                          title={isFiltered ? 'Nothing matches those filters' : 'No submissions yet'}
                          description={
                            isFiltered
                              ? 'Try a different search term or status.'
                              : 'Once students submit work, it will show up here for marking.'
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((submission) => (
                      <TableRow
                        key={submission.id}
                        className="cursor-pointer"
                        onClick={() => router.push(`/submissions/${submission.id}`)}
                      >
                        <TableCell>
                          <div className="flex items-center gap-2.5">
                            <Avatar className="size-7">
                              <AvatarFallback className="text-[11px]">
                                {initials(submission.studentName)}
                              </AvatarFallback>
                            </Avatar>
                            <span className="font-medium">{submission.studentName}</span>
                          </div>
                        </TableCell>
                        <TableCell className="max-w-[220px] truncate">
                          {submission.assignmentTitle}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatRelative(submission.submittedAtUtc)}
                        </TableCell>
                        <TableCell>
                          <SubmissionStatusBadge status={submission.status} />
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {formatMarks(submission.marks, submission.marksOutOf)}
                        </TableCell>
                        <TableCell className="text-right">
                          <span className="inline-flex items-center gap-0.5 text-xs font-medium text-primary">
                            {readOnly || submission.status === 'Graded' ? 'View' : 'Mark'}
                            <ChevronRight className="size-3.5" />
                          </span>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>

            {query.data && (
              <PaginationBar
                pagination={query.data.pagination}
                onPageChange={setPage}
                itemLabel="submissions"
              />
            )}
          </>
        )}
      </div>
    </div>
  );
}
