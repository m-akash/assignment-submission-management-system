'use client';

import { useState } from 'react';
import { GraduationCap, UserRound } from 'lucide-react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { useMyStudentCourses } from '@/hooks/use-admin-resources';
import type { StudentCourse } from '@/types/api';

export default function EnrolledCoursesPage() {
  return (
    <RoleGuard allow={['Student']}>
      <EnrolledCoursesView />
    </RoleGuard>
  );
}

function EnrolledCoursesView() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const query = useMyStudentCourses({ search, page, pageSize: 10 });
  const items = query.data?.items ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Coursework"
        title="My courses"
        icon={GraduationCap}
        description="The courses you are taking and the teacher for each."
      />

      <div className="panel overflow-hidden">
        <div className="border-b p-4">
          <SearchInput
            value={search}
            onChange={(value) => {
              setSearch(value);
              setPage(1);
            }}
            placeholder="Search course, class or teacher…"
            className="sm:max-w-xs"
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
                    <TableHead>Course</TableHead>
                    <TableHead>Code</TableHead>
                    <TableHead>Class</TableHead>
                    <TableHead>Teacher</TableHead>
                    <TableHead>Email</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={5} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={5} className="p-0">
                        <EmptyState
                          icon={GraduationCap}
                          title={search ? 'Nothing matches that search' : 'No courses yet'}
                          description={
                            search
                              ? 'Try a different search term.'
                              : 'Once an admin enrols you in a class, its courses and teachers will appear here.'
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((course) => (
                      <TableRow key={course.id}>
                        <TableCell className="truncate font-medium">{course.courseName}</TableCell>
                        <TableCell className="font-mono text-xs text-muted-foreground">
                          {course.courseCode}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {course.className}
                        </TableCell>
                        <TableCell>
                          <TeacherNamesCell course={course} />
                        </TableCell>
                        <TableCell>
                          <TeacherEmailsCell course={course} />
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
                itemLabel="courses"
              />
            )}
          </>
        )}
      </div>
    </div>
  );
}

function TeacherNamesCell({ course }: { course: StudentCourse }) {
  if (course.teachers.length === 0) {
    return <span className="text-sm text-muted-foreground">Not yet assigned</span>;
  }

  return (
    <div className="flex flex-col gap-0.5">
      {course.teachers.map((teacher) => (
        <span key={teacher.teacherId} className="inline-flex items-center gap-1.5 text-sm">
          <UserRound className="size-3.5 text-muted-foreground" />
          <span className="font-medium">{teacher.teacherName}</span>
        </span>
      ))}
    </div>
  );
}

function TeacherEmailsCell({ course }: { course: StudentCourse }) {
  if (course.teachers.length === 0) {
    return <span className="text-sm text-muted-foreground">—</span>;
  }

  return (
    <div className="flex flex-col gap-0.5">
      {course.teachers.map((teacher) => (
        <span key={teacher.teacherId} className="text-xs text-muted-foreground">
          {teacher.teacherEmail}
        </span>
      ))}
    </div>
  );
}
