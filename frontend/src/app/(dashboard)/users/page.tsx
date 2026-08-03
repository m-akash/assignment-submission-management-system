'use client';

import { useState } from 'react';
import { MoreHorizontal, Pencil, Plus, Trash2, Users } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ConfirmDialog } from '@/components/shared/confirm-dialog';
import { FilterSelect } from '@/components/shared/filter-select';
import { PageHeader } from '@/components/shared/page-header';
import { PaginationBar } from '@/components/shared/pagination-bar';
import { RoleGuard } from '@/components/shared/role-guard';
import { SearchInput } from '@/components/shared/search-input';
import { EmptyState, ErrorState, TableSkeleton } from '@/components/shared/states';
import { RoleBadge } from '@/components/shared/status-badge';
import { UserFormDialog } from '@/components/features/admin/user-form-dialog';
import { useClassOptions, useDeleteUser, useUsers } from '@/hooks/use-admin-resources';
import { initials } from '@/lib/format';
import type { Role, User } from '@/types/api';

const ROLE_OPTIONS = [
  { value: 'Admin', label: 'Admin' },
  { value: 'Teacher', label: 'Teacher' },
  { value: 'Student', label: 'Student' },
];

export default function UsersPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <UsersView />
    </RoleGuard>
  );
}

function UsersView() {
  const [search, setSearch] = useState('');
  const [role, setRole] = useState<Role | ''>('');
  const [classId, setClassId] = useState('');
  const [page, setPage] = useState(1);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<User | null>(null);
  const [deleting, setDeleting] = useState<User | null>(null);

  const classes = useClassOptions();
  const remove = useDeleteUser();
  const query = useUsers({ search, role, classId, page, pageSize: 10 });

  const items = query.data?.items ?? [];
  const isFiltered = !!search || !!role || !!classId;
  const classOptions = (classes.data ?? []).map((c) => ({ value: c.id, label: c.name }));

  function withPageReset<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Users"
        description="Admins, teachers and students. Accounts are created here — self-registration is disabled."
        actions={
          <Button onClick={openCreate}>
            <Plus className="size-4" />
            Create user
          </Button>
        }
      />

      <div className="rounded-xl border bg-card">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search name or email…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            value={role}
            onChange={withPageReset((value: string) => setRole(value as Role | ''))}
            options={ROLE_OPTIONS}
            allLabel="All roles"
          />
          <FilterSelect
            value={classId}
            onChange={withPageReset(setClassId)}
            options={classOptions}
            allLabel="All classes"
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
                    <TableHead>Email</TableHead>
                    <TableHead>Role</TableHead>
                    <TableHead>Class</TableHead>
                    <TableHead>Student ID</TableHead>
                    <TableHead className="w-10" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={6} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="p-0">
                        <EmptyState
                          icon={Users}
                          title={isFiltered ? 'Nothing matches those filters' : 'No users yet'}
                          description={
                            isFiltered ? 'Try a different search term.' : 'Create the first account.'
                          }
                          action={
                            !isFiltered && (
                              <Button size="sm" onClick={openCreate}>
                                <Plus className="size-4" />
                                Create user
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((user) => (
                      <TableRow key={user.id}>
                        <TableCell>
                          <div className="flex items-center gap-2.5">
                            <Avatar className="size-7">
                              <AvatarFallback className="text-[11px]">
                                {initials(user.fullName)}
                              </AvatarFallback>
                            </Avatar>
                            <span className="font-medium">{user.fullName}</span>
                          </div>
                        </TableCell>
                        <TableCell className="text-muted-foreground">{user.email}</TableCell>
                        <TableCell>
                          <RoleBadge role={user.role} />
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {user.className ?? '—'}
                        </TableCell>
                        <TableCell className="font-mono text-sm text-muted-foreground">
                          {user.studentId ?? '—'}
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" aria-label={`Actions for ${user.fullName}`}>
                                <MoreHorizontal className="size-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem
                                onClick={() => {
                                  setEditing(user);
                                  setFormOpen(true);
                                }}
                              >
                                <Pencil className="size-4" />
                                Edit
                              </DropdownMenuItem>
                              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(user)}>
                                <Trash2 className="size-4" />
                                Deactivate
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
              <PaginationBar pagination={query.data.pagination} onPageChange={setPage} itemLabel="users" />
            )}
          </>
        )}
      </div>

      <UserFormDialog open={formOpen} onOpenChange={setFormOpen} user={editing} />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Deactivate this user?"
        description={`"${deleting?.fullName}" will no longer be able to sign in. Their existing records are kept.`}
        confirmLabel="Deactivate"
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
