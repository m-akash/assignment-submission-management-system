'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { GraduationCap, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/shared/page-header';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { useMyTeacherMappings, useClassOptions } from '@/hooks/use-admin-resources';
import type { TeacherMapping } from '@/types/api';

export default function MyCoursesPage() {
  return (
    <RoleGuard allow={['Teacher']}>
      <MyCoursesView />
    </RoleGuard>
  );
}

function MyCoursesView() {
  const [search, setSearch] = useState('');

  const mappings = useMyTeacherMappings();
  const classes = useClassOptions();

  const countByClass = useMemo(
    () => new Map((classes.data ?? []).map((c) => [c.id, c.studentCount])),
    [classes.data],
  );

  const items = (mappings.data ?? []).filter((m) => matchesSearch(m, search));
  const isLoading = mappings.isLoading || classes.isLoading;

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/', label: 'Dashboard' }}
        eyebrow="Coursework"
        title="My courses & classes"
        description="The courses you teach and the students in each class."
      />

      <div className="panel overflow-hidden">
        <div className="border-b p-4">
          <SearchInput
            value={search}
            onChange={setSearch}
            placeholder="Search course or class…"
            className="sm:max-w-xs"
          />
        </div>

        {mappings.isError ? (
          <ErrorState message={mappings.error instanceof Error ? mappings.error.message : undefined} />
        ) : (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Course</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>Class</TableHead>
                  <TableHead className="text-right">Students</TableHead>
                  <TableHead className="w-40">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  <TableSkeleton columns={5} />
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="p-0">
                      <EmptyState
                        icon={GraduationCap}
                        title={search ? 'Nothing matches that search' : 'No courses assigned yet'}
                        description={
                          search
                            ? 'Try a different search term.'
                            : 'Once an admin assigns you to a course and class, they will appear here.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((mapping) => {
                    const studentCount = countByClass.get(mapping.classId) ?? 0;
                    return (
                      <TableRow key={mapping.id}>
                        <TableCell className="truncate font-medium">{mapping.courseName}</TableCell>
                        <TableCell className="font-mono text-xs text-muted-foreground">
                          {mapping.courseCode}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">{mapping.className}</TableCell>
                        <TableCell className="text-right">
                          <Link
                            href={`/my-students?classId=${mapping.classId}`}
                            className="inline-flex items-center gap-1.5 font-medium underline-offset-4 hover:underline"
                          >
                            <Users className="size-3.5 text-muted-foreground" />
                            {studentCount}
                          </Link>
                        </TableCell>
                        <TableCell>
                          <Button asChild size="sm" variant="outline">
                            <Link
                              href={`/submissions?courseId=${mapping.courseId}&classId=${mapping.classId}`}
                            >
                              View work
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </div>
    </div>
  );
}

function matchesSearch(mapping: TeacherMapping, search: string): boolean {
  const term = search.trim().toLowerCase();
  if (!term) return true;
  return (
    mapping.courseName.toLowerCase().includes(term) ||
    mapping.courseCode.toLowerCase().includes(term) ||
    mapping.className.toLowerCase().includes(term)
  );
}
