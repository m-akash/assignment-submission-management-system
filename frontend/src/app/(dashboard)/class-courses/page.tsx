'use client';

import { useState } from 'react';
import { Layers, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ClassFilter } from '@/components/shared/class-picker';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { ClassCourseFormDrawer } from '@/components/features/admin/class-course-form-drawer';
import {
  useClassCourses,
  useClassOptions,
  useDeleteClassCourse,
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
 * and an assignment can only be created against one. The teacher and assignment counts are
 * shown because an offering with no teacher is inert, and one with assignments cannot be
 * removed.
 */
function ClassCoursesView() {
  const [search, setSearch] = useState('');
  const [classIds, setClassIds] = useState<string[]>([]);
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [deleting, setDeleting] = useState<ClassCourse | null>(null);

  const classes = useClassOptions();
  const remove = useDeleteClassCourse();
  const query = useClassCourses({ search, classId: classIds, page, pageSize: 10 });
  const items = query.data?.items ?? [];
  const isFiltered = !!search || classIds.length > 0;

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
        <div className="flex flex-col gap-3 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={(value) => {
              setSearch(value);
              setPage(1);
            }}
            placeholder="Search by grade, section or course…"
            className="sm:max-w-xs"
          />
          <ClassFilter
            classes={classes.data ?? []}
            loading={classes.isLoading}
            onChange={(values) => {
              setClassIds(values);
              setPage(1);
            }}
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
                    <TableHead>Assignments</TableHead>
                    <TableHead className="w-20">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={7} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={7} className="p-0">
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
                          {/* Zero teachers means nobody can set work for this offering yet —
                              worth flagging rather than showing a bare 0. */}
                          {offering.teacherCount === 0 ? (
                            <span className="text-sm text-muted-foreground">
                              None assigned yet
                            </span>
                          ) : (
                            offering.teacherCount
                          )}
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {offering.assignmentCount}
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
