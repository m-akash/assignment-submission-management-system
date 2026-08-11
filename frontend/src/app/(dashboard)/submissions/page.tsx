'use client';

import { Suspense, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { ChevronRight, Inbox } from 'lucide-react';
import { useAuth } from '@/context/AuthContext';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ClassFilter } from '@/components/shared/class-picker';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { SubmissionStatusBadge } from '@/components/shared/status-badge';
import { useSubmissions } from '@/hooks/use-submissions';
import {
  useClassOptions,
  useCourseOptions,
  useMyTeacherMappings,
} from '@/hooks/use-admin-resources';
import { distinctClasses } from '@/lib/classes';
import { distinctCourses } from '@/lib/courses';
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
  // query string; after that the dropdown owns it, and unknown values are ignored.
  const [statuses, setStatuses] = useState<SubmissionStatus[]>(() =>
    searchParams
      .getAll('status')
      .filter((value): value is SubmissionStatus =>
        STATUS_OPTIONS.some((option) => option.value === value),
      ),
  );
  // Seeded the same way, so "View work" on a teacher's course row lands here already
  // narrowed to that class and course.
  const [initialClassIds] = useState(() => searchParams.getAll('classId'));
  const [classIds, setClassIds] = useState<string[]>(initialClassIds);
  const [courseIds, setCourseIds] = useState<string[]>(() => searchParams.getAll('courseId'));
  const [page, setPage] = useState(1);

  // Where the two dropdowns get their rows. A teacher is offered only what they teach —
  // the list is scoped to their own assignments anyway, so the rest would match nothing.
  // An admin sees every submission in the school, so they are offered the whole catalogue.
  const mappings = useMyTeacherMappings(!readOnly);
  const allClasses = useClassOptions(readOnly);
  const allCourses = useCourseOptions(readOnly);

  const classOptions = readOnly ? (allClasses.data ?? []) : distinctClasses(mappings.data ?? []);
  const courseOptions = readOnly
    ? (allCourses.data ?? []).map((course) => ({
        id: course.id,
        name: course.name,
        code: course.code,
      }))
    : distinctCourses(mappings.data ?? []);
  const optionsLoading = readOnly
    ? allClasses.isLoading || allCourses.isLoading
    : mappings.isLoading;

  const query = useSubmissions({
    search,
    status: statuses,
    assignmentId,
    classId: classIds,
    courseId: courseIds,
    page,
    pageSize: 10,
  });
  const items = query.data?.items ?? [];
  const isFiltered =
    !!search || statuses.length > 0 || classIds.length > 0 || courseIds.length > 0;

  function withPageReset<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  return (
    <div className="space-y-6">
      <PageHeader
        // Filtered views are reached from the assignment itself, so that is where back goes.
        back={
          assignmentId
            ? { href: `/assignments/${assignmentId}`, label: 'Back to assignment' }
            : { href: '/', label: 'Dashboard' }
        }
        eyebrow="Coursework"
        title="Submissions"
        description={
          assignmentId
            ? 'Filtered to one assignment. Clear the filter to see everything.'
            : 'Every submission for the assignments you teach.'
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row sm:flex-wrap">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search student or assignment…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            values={statuses}
            onChange={withPageReset((values: string[]) => setStatuses(values as SubmissionStatus[]))}
            options={STATUS_OPTIONS}
            allLabel="Any status"
            className="w-44"
          />
          <FilterSelect
            values={courseIds}
            onChange={withPageReset(setCourseIds)}
            options={courseOptions.map((course) => ({ value: course.id, label: course.name }))}
            allLabel="All courses"
            disabled={optionsLoading}
            className="w-44"
          />
          <ClassFilter
            classes={classOptions}
            loading={optionsLoading}
            initialClassIds={initialClassIds}
            onChange={withPageReset(setClassIds)}
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
                              ? 'Try a different search term, course, class or status.'
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
