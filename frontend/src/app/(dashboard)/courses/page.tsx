'use client';

import { useState } from 'react';
import { BookOpen, MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { CourseFormDialog } from '@/components/features/admin/course-form-dialog';
import { useDeleteCourse, useCourses } from '@/hooks/use-admin-resources';
import type { Course } from '@/types/api';

export default function CoursesPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <CoursesView />
    </RoleGuard>
  );
}

function CoursesView() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Course | null>(null);
  const [deleting, setDeleting] = useState<Course | null>(null);

  const remove = useDeleteCourse();
  const query = useCourses({ search, page, pageSize: 10 });
  const items = query.data?.items ?? [];

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Administration"
        title="Courses"
        icon={BookOpen}
        description="Courses are linked to classes through a teaching assignment."
        actions={
          <Button onClick={openCreate}>
            <Plus className="size-4" />
            Create course
          </Button>
        }
      />

      <div className="panel overflow-hidden">
        <div className="border-b p-4">
          <SearchInput
            value={search}
            onChange={(value) => {
              setSearch(value);
              setPage(1);
            }}
            placeholder="Search courses…"
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
                    <TableHead>Name</TableHead>
                    <TableHead>Code</TableHead>
                    <TableHead className="w-20">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={3} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={3} className="p-0">
                        <EmptyState
                          icon={BookOpen}
                          title={search ? 'Nothing matches that search' : 'No courses yet'}
                          description={search ? undefined : 'Create the first course to get started.'}
                          action={
                            !search && (
                              <Button size="sm" onClick={openCreate}>
                                <Plus className="size-4" />
                                Create course
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((course) => (
                      <TableRow key={course.id}>
                        <TableCell className="font-medium">{course.name}</TableCell>
                        <TableCell>
                          <Badge variant="secondary" className="font-mono">
                            {course.code}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" aria-label={`Actions for ${course.name}`}>
                                <MoreHorizontal className="size-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem
                                onClick={() => {
                                  setEditing(course);
                                  setFormOpen(true);
                                }}
                              >
                                <Pencil className="size-4" />
                                Edit
                              </DropdownMenuItem>
                              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(course)}>
                                <Trash2 className="size-4" />
                                Delete
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
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

      <CourseFormDialog open={formOpen} onOpenChange={setFormOpen} course={editing} />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Delete this course?"
        description={`"${deleting?.name}" can only be deleted if no teaching assignments reference it.`}
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
