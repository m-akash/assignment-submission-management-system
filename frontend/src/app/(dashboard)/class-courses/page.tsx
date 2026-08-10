'use client';

import { useState } from 'react';
import { Layers, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ClassFilter } from '@/components/shared/class-picker';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { ClassCourseFormDrawer } from '@/components/features/admin/class-course-form-drawer';
import {
  useClassCourses,
  useClassOptions,
  useCourseOptions,
  useDeleteClassCourse,
  useUsers,
} from '@/hooks/use-admin-resources';
import { classLabel, gradeLabel, sectionLabel } from '@/lib/format';
import type { ClassCourse } from '@/types/api';

export default function ClassCoursesPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <ClassCoursesView />
    </RoleGuard>
  );
}

/**
 * Course offerings — which courses each class studies.
 *
 * The catalogue everything else is scoped to: a teacher can only be assigned to an offering,
 * and an assignment can only be created against one. The assigned teachers and the assignment
 * count are shown because an offering with no teacher is inert, and one with assignments cannot
 * be removed.
 */
function ClassCoursesView() {
  const [search, setSearch] = useState('');
  const [classIds, setClassIds] = useState<string[]>([]);
  const [courseIds, setCourseIds] = useState<string[]>([]);
  const [teacherIds, setTeacherIds] = useState<string[]>([]);
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [deleting, setDeleting] = useState<ClassCourse | null>(null);

  const classes = useClassOptions();
  const courses = useCourseOptions();
  const teachers = useUsers({ role: 'Teacher', pageSize: 100 });
  const remove = useDeleteClassCourse();
  const query = useClassCourses({
    search,
    classId: classIds,
    courseId: courseIds,
    teacherId: teacherIds,
    page,
    pageSize: 10,
  });
  const items = query.data?.items ?? [];
  const isFiltered =
    !!search || classIds.length > 0 || courseIds.length > 0 || teacherIds.length > 0;
  // Every teacher, not only the mapped ones — an unmapped teacher is a legitimate thing to
  // filter by, and its empty result is the answer.
  const teacherOptions = (teachers.data?.items ?? []).map((teacher) => ({
    value: teacher.id,
    label: teacher.fullName,
  }));
  const courseOptions = (courses.data ?? []).map((course) => ({
    value: course.id,
    label: course.name,
  }));

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
        eyebrow="Administration"
        title="Course Offerings"
        description="Which courses each class studies. Teachers are assigned to an offering, and assignments are created against one."
        actions={
          <Button onClick={() => setFormOpen(true)}>
            <Plus className="size-4" />
            Add course to class
          </Button>
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row sm:flex-wrap">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search by grade, section or course…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            values={teacherIds}
            onChange={withPageReset(setTeacherIds)}
            options={teacherOptions}
            allLabel="All teachers"
            disabled={teachers.isLoading}
          />
          <FilterSelect
            values={courseIds}
            onChange={withPageReset(setCourseIds)}
            options={courseOptions}
            allLabel="All courses"
            disabled={courses.isLoading}
          />
          <ClassFilter
            classes={classes.data ?? []}
            loading={classes.isLoading}
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
                    <TableHead>Class</TableHead>
                    <TableHead>Section</TableHead>
                    <TableHead>Course</TableHead>
                    <TableHead>Code</TableHead>
                    <TableHead>Teachers</TableHead>
                    <TableHead className="w-20">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={6} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="p-0">
                        <EmptyState
                          icon={Layers}
                          title={
                            isFiltered ? 'Nothing matches those filters' : 'No offerings yet'
                          }
                          description={
                            isFiltered
                              ? undefined
                              : 'Add a course to a class so teachers can be assigned to it.'
                          }
                          action={
                            !isFiltered && (
                              <Button size="sm" onClick={() => setFormOpen(true)}>
                                <Plus className="size-4" />
                                Add course to class
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((offering) => (
                      <TableRow key={offering.id}>
                        <TableCell className="font-medium">
                          {gradeLabel(offering.classLevel)}
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {sectionLabel(offering.classSection)}
                        </TableCell>
                        <TableCell className="font-medium">{offering.courseName}</TableCell>
                        <TableCell className="font-mono text-xs text-muted-foreground">
                          {offering.courseCode}
                        </TableCell>
                        <TableCell>
                          {/* Who teaches this offering, by name — a count answers a question
                              nobody asks. Zero teachers means nobody can set work for it yet,
                              which is worth saying rather than leaving the cell blank. */}
                          {offering.teacherNames.length === 0 ? (
                            <span className="text-sm text-muted-foreground">
                              None assigned yet
                            </span>
                          ) : (
                            offering.teacherNames.join(', ')
                          )}
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove ${offering.courseName} from ${classLabel(offering.classLevel, offering.classSection)}`}
                            onClick={() => setDeleting(offering)}
                          >
                            <Trash2 className="size-4" />
                          </Button>
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
                itemLabel="offerings"
              />
            )}
          </>
        )}
      </div>

      <ClassCourseFormDrawer open={formOpen} onOpenChange={setFormOpen} />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Remove this offering?"
        description={
          deleting
            ? `${classLabel(deleting.classLevel, deleting.classSection)} will no longer study ${deleting.courseName}. This is refused while any teacher is assigned to it or any assignment exists for it.`
            : ''
        }
        pending={remove.isPending}
        onConfirm={() => {
          if (deleting) {
            remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) });
          }
        }}
      />
    </div>
  );
}
