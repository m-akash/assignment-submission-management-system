'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Backpack } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { useEnrollments, useMyTeacherMappings } from '@/hooks/use-admin-resources';
import { initials } from '@/lib/format';

export default function MyStudentsPage() {
  return (
    <RoleGuard allow={['Teacher']}>
      <Suspense>
        <MyStudentsView />
      </Suspense>
    </RoleGuard>
  );
}

function MyStudentsView() {
  const searchParams = useSearchParams();
  // Arriving from a course card deep-links one class; the filter holds any number after that.
  const initialClassIds = searchParams.getAll('classId');

  const mappings = useMyTeacherMappings();

  // Distinct classes the teacher actually teaches — the only ones they may see students for.
  const classOptions = [
    ...new Map((mappings.data ?? []).map((m) => [m.classId, m.className])).entries(),
  ].map(([value, label]) => ({ value, label }));

  const [search, setSearch] = useState('');
  const [classIds, setClassIds] = useState<string[]>(initialClassIds);
  const [page, setPage] = useState(1);

  const query = useEnrollments(
    { search, classId: classIds, page, pageSize: 10 },
    // Avoid firing before the class list resolves when arriving with a ?classId — and always
    // need a class to query, since the server scopes a teacher to taught classes.
    { enabled: mappings.isSuccess },
  );
  const items = query.data?.items ?? [];
  const isFiltered = !!search || classIds.length > 0;

  function withPageReset<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/', label: 'Dashboard' }}
        eyebrow="Coursework"
        title="My students"
        description={
          classIds.length > 0
            ? `Filtered to ${classIds.length === 1 ? 'one class' : `${classIds.length} classes`}. Choose others, or clear the filter.`
            : 'The students in each class you teach. Pick a class to begin.'
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search student name or email…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            values={classIds}
            onChange={withPageReset(setClassIds)}
            options={classOptions}
            allLabel="All my classes"
            className="w-full sm:w-[200px]"
            disabled={!mappings.isSuccess}
          />
        </div>

        {query.isError ? (
          <ErrorState message={query.error instanceof Error ? query.error.message : undefined} />
        ) : mappings.isLoading ? (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Student</TableHead>
                  <TableHead>Student ID</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Class</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableSkeleton columns={4} />
              </TableBody>
            </Table>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Student</TableHead>
                    <TableHead>Student ID</TableHead>
                    <TableHead>Email</TableHead>
                    <TableHead>Class</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={4} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4} className="p-0">
                        <EmptyState
                          icon={Backpack}
                          title={isFiltered ? 'Nothing matches those filters' : 'No students yet'}
                          description={
                            isFiltered
                              ? 'Try a different search term or class.'
                              : 'Once students are enrolled in your classes, they will appear here.'
                          }
                          action={
                            !isFiltered && (
                              <Link href="/my-courses" className="text-sm font-medium text-primary hover:underline">
                                See your courses
                              </Link>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((enrollment) => (
                      <TableRow key={enrollment.id}>
                        <TableCell>
                          <div className="flex items-center gap-2.5">
                            <Avatar className="size-7">
                              <AvatarFallback className="text-[11px]">
                                {initials(enrollment.studentName)}
                              </AvatarFallback>
                            </Avatar>
                            <span className="font-medium">{enrollment.studentName}</span>
                          </div>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {enrollment.studentNumber ?? '—'}
                        </TableCell>
                        <TableCell className="max-w-[220px] truncate text-sm text-muted-foreground">
                          {enrollment.studentEmail}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">{enrollment.className}</TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>

            {query.data && (
              <PaginationBar pagination={query.data.pagination} onPageChange={setPage} itemLabel="students" />
            )}
          </>
        )}
      </div>
    </div>
  );
}
