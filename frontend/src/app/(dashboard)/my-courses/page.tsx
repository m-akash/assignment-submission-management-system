'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { GraduationCap, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ClassFilter } from '@/components/shared/class-picker';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { useMyTeacherMappings, useClassOptions } from '@/hooks/use-admin-resources';
import { distinctClasses } from '@/lib/classes';
import { distinctCourses } from '@/lib/courses';
import { gradeLabel, sectionLabel } from '@/lib/format';
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
  const [courseIds, setCourseIds] = useState<string[]>([]);
  const [classIds, setClassIds] = useState<string[]>([]);

  const mappings = useMyTeacherMappings();
  const classes = useClassOptions();

  const countByClass = useMemo(
    () => new Map((classes.data ?? []).map((c) => [c.id, c.studentCount])),
    [classes.data],
  );

  // The whole set of mappings is already in hand — this list is one page, never paged —
  // so the dropdowns narrow it here rather than asking the server again. Their rows come
  // from the same set, which is what keeps them offering only combinations that exist.
  const taughtClasses = distinctClasses(mappings.data ?? []);
  const taughtCourses = distinctCourses(mappings.data ?? []);

  const items = (mappings.data ?? []).filter(
    (m) =>
      matchesSearch(m, search) &&
      (courseIds.length === 0 || courseIds.includes(m.courseId)) &&
      (classIds.length === 0 || classIds.includes(m.classId)),
  );
  const isLoading = mappings.isLoading || classes.isLoading;
  const isFiltered = !!search || courseIds.length > 0 || classIds.length > 0;

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/', label: 'Dashboard' }}
        eyebrow="Coursework"
        title="My courses & classes"
        description="The courses you teach and the students in each class."
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row sm:flex-wrap">
          <SearchInput
            value={search}
            onChange={setSearch}
            placeholder="Search course or class…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            values={courseIds}
            onChange={setCourseIds}
            options={taughtCourses.map((course) => ({ value: course.id, label: course.name }))}
            allLabel="All courses"
            disabled={mappings.isLoading}
            className="w-44"
          />
          <FilterSelect
            values={courseIds}
            onChange={setCourseIds}
            options={taughtCourses.map((course) => ({ value: course.id, label: course.code }))}
            allLabel="All codes"
            disabled={mappings.isLoading}
            className="w-36"
          />
          <ClassFilter
            classes={taughtClasses}
            loading={mappings.isLoading}
            onChange={setClassIds}
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
                  <TableHead>Section</TableHead>
                  <TableHead className="text-right">Students</TableHead>
                  <TableHead className="w-40">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  <TableSkeleton columns={6} />
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="p-0">
                      <EmptyState
                        icon={GraduationCap}
                        title={isFiltered ? 'Nothing matches those filters' : 'No courses assigned yet'}
                        description={
                          isFiltered
                            ? 'Try a different search term, course or class.'
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
                        <TableCell className="text-sm">{gradeLabel(mapping.classLevel)}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {sectionLabel(mapping.classSection)}
                        </TableCell>
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
    // A number matches the grade, a letter the section — the two halves of a class, searched
    // the same way the server searches them.
    String(mapping.classLevel) === term ||
    (mapping.classSection?.toLowerCase().includes(term) ?? false)
  );
}
