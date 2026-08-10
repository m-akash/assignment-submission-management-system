'use client';

import { useState } from 'react';
import { CalendarRange, MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react';
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
import { AcademicYearFormDrawer } from '@/components/features/admin/academic-year-form-drawer';
import { useAcademicYears, useDeleteAcademicYear } from '@/hooks/use-admin-resources';
import { formatDate } from '@/lib/format';
import type { AcademicYear } from '@/types/api';

export default function AcademicYearsPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <AcademicYearsView />
    </RoleGuard>
  );
}

function AcademicYearsView() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<AcademicYear | null>(null);
  const [deleting, setDeleting] = useState<AcademicYear | null>(null);

  const remove = useDeleteAcademicYear();
  const query = useAcademicYears({ search, page, pageSize: 10 });
  const items = query.data?.items ?? [];

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  // The server refuses a year with enrollments, so the dialog says so up front rather than
  // letting the admin find out from a toast.
  const deletable = !deleting || deleting.enrollmentCount === 0;

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/', label: 'Dashboard' }}
        eyebrow="Administration"
        title="Academic years"
        description="The sessions students are enrolled into. One is the current year, which enrollment forms open on."
        actions={
          <Button onClick={openCreate}>
            <Plus className="size-4" />
            Create academic year
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
            placeholder="Search academic years…"
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
                    <TableHead>Starts</TableHead>
                    <TableHead>Ends</TableHead>
                    <TableHead>Enrollments</TableHead>
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
                          icon={CalendarRange}
                          title={search ? 'Nothing matches that search' : 'No academic years yet'}
                          description={
                            search
                              ? undefined
                              : 'Create the first session before enrolling any students.'
                          }
                          action={
                            !search && (
                              <Button size="sm" onClick={openCreate}>
                                <Plus className="size-4" />
                                Create academic year
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((year) => (
                      <TableRow key={year.id}>
                        <TableCell className="font-medium">
                          <span className="flex items-center gap-2">
                            {year.name}
                            {year.isCurrent && <Badge>Current</Badge>}
                          </span>
                        </TableCell>
                        <TableCell>{formatDate(year.startDate)}</TableCell>
                        <TableCell>{formatDate(year.endDate)}</TableCell>
                        <TableCell>
                          <Badge variant="secondary">{year.enrollmentCount}</Badge>
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button
                                variant="ghost"
                                size="icon"
                                aria-label={`Actions for ${year.name}`}
                              >
                                <MoreHorizontal className="size-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem
                                onClick={() => {
                                  setEditing(year);
                                  setFormOpen(true);
                                }}
                              >
                                <Pencil className="size-4" />
                                Edit
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                variant="destructive"
                                onClick={() => setDeleting(year)}
                              >
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
                itemLabel="academic years"
              />
            )}
          </>
        )}
      </div>

      <AcademicYearFormDrawer
        open={formOpen}
        onOpenChange={setFormOpen}
        academicYear={editing}
      />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={deletable ? 'Delete this academic year?' : 'This year cannot be deleted'}
        description={
          deletable
            ? `"${deleting?.name}" has no enrollments and can be removed.`
            : `"${deleting?.name}" has ${deleting?.enrollmentCount} enrollment${
                deleting?.enrollmentCount === 1 ? '' : 's'
              } recorded against it. Those rows name this year, so it has to stay.`
        }
        confirmLabel={deletable ? 'Delete' : 'Close'}
        pending={remove.isPending}
        onConfirm={() => {
          if (!deleting) return;
          if (!deletable) {
            setDeleting(null);
            return;
          }
          remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) });
        }}
      />
    </div>
  );
}
