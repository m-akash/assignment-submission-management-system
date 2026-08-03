'use client';

import { useState } from 'react';
import { GraduationCap, MoreHorizontal, Pencil, Plus, Trash2, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ClassFormDialog } from '@/components/features/admin/class-form-dialog';
import { ClassRosterDialog } from '@/components/features/admin/class-roster-dialog';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { useClasses, useDeleteClass } from '@/hooks/use-admin-resources';
import type { ClassRoom } from '@/types/api';

export default function ClassesPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <ClassesView />
    </RoleGuard>
  );
}

function ClassesView() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<ClassRoom | null>(null);
  const [deleting, setDeleting] = useState<ClassRoom | null>(null);
  const [viewingRoster, setViewingRoster] = useState<ClassRoom | null>(null);

  const remove = useDeleteClass();
  const query = useClasses({ search, page, pageSize: 10 });
  const items = query.data?.items ?? [];

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Classes"
        description="A student belongs to exactly one class. Create classes before assigning teachers to them."
        actions={
          <Button onClick={openCreate}>
            <Plus className="size-4" />
            Create class
          </Button>
        }
      />

      <div className="rounded-xl border bg-card">
        <div className="border-b p-4">
          <SearchInput
            value={search}
            onChange={(value) => {
              setSearch(value);
              setPage(1);
            }}
            placeholder="Search classes…"
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
                    <TableHead>Grade</TableHead>
                    <TableHead>Section</TableHead>
                    <TableHead>Students</TableHead>
                    <TableHead className="w-20">Action</TableHead>
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
                          title={search ? 'Nothing matches that search' : 'No classes yet'}
                          description={search ? undefined : 'Create the first class to get started.'}
                          action={
                            !search && (
                              <Button size="sm" onClick={openCreate}>
                                <Plus className="size-4" />
                                Create class
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((classRoom) => (
                      <TableRow key={classRoom.id}>
                        <TableCell className="font-medium">{classRoom.name}</TableCell>
                        <TableCell className="text-muted-foreground">{classRoom.grade ?? '—'}</TableCell>
                        <TableCell className="text-muted-foreground">{classRoom.section ?? '—'}</TableCell>
                        <TableCell>
                          <button
                            type="button"
                            onClick={() => setViewingRoster(classRoom)}
                            className="inline-flex items-center gap-1.5 rounded-md text-sm font-medium underline-offset-4 hover:underline"
                          >
                            <Users className="size-3.5 text-muted-foreground" />
                            {classRoom.studentCount}
                          </button>
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" aria-label={`Actions for ${classRoom.name}`}>
                                <MoreHorizontal className="size-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => setViewingRoster(classRoom)}>
                                <Users className="size-4" />
                                View students
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                onClick={() => {
                                  setEditing(classRoom);
                                  setFormOpen(true);
                                }}
                              >
                                <Pencil className="size-4" />
                                Edit
                              </DropdownMenuItem>
                              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(classRoom)}>
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
              <PaginationBar pagination={query.data.pagination} onPageChange={setPage} itemLabel="classes" />
            )}
          </>
        )}
      </div>

      <ClassFormDialog open={formOpen} onOpenChange={setFormOpen} classRoom={editing} />

      <ClassRosterDialog
        open={!!viewingRoster}
        onOpenChange={(open) => !open && setViewingRoster(null)}
        classRoom={viewingRoster}
      />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Delete this class?"
        description={`"${deleting?.name}" can only be deleted if no students or teaching assignments reference it.`}
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
