'use client';

import { Suspense, useEffect, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
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

/**
 * This page doubles as the Teachers and Students screens, so it names itself after the
 * role in the URL instead of always saying "Users".
 */
const HEADINGS: Record<Role | '', { title: string; description: string; action: string }> = {
  '': {
    title: 'Users',
    description:
      'Admins, teachers and students. Accounts are created here — self-registration is disabled.',
    action: 'Create user',
  },
  Admin: {
    title: 'Admins',
    description: 'Accounts that can manage every part of the school.',
    action: 'Create admin',
  },
  Teacher: {
    title: 'Teachers',
    description: 'Assign a teacher to a class and course before they can set any work.',
    action: 'Create teacher',
  },
  Student: {
    title: 'Students',
    description: 'Every student belongs to one class and is given a student ID from it.',
    action: 'Create student',
  },
};

export default function UsersPage() {
  return (
    <RoleGuard allow={['Admin']}>
      {/* useSearchParams opts this subtree out of prerendering, so give it a boundary. */}
      <Suspense fallback={<TableSkeleton columns={6} />}>
        <UsersView />
      </Suspense>
    </RoleGuard>
  );
}

function UsersView() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // The role filter lives in the URL, not component state: the sidebar's Teachers and
  // Students links and the dashboard tiles deep-link straight into a filtered view, and
  // it keeps a filtered list shareable and survivable across a reload. It is read with
  // getAll, so ?role=Teacher&role=Student selects both and a one-role link still works.
  // Anything the URL offers that is not a real role is dropped — a hand-typed ?role=Foo
  // must not be able to break the page.
  const roles = searchParams
    .getAll('role')
    .filter((value): value is Role => ROLE_OPTIONS.some((option) => option.value === value));
  // The page names itself after the role, which only means something while exactly one is
  // selected: "Teachers" is the wrong heading for a list of teachers and students, so a
  // wider selection falls back to the neutral "Users" one.
  const role: Role | '' = roles.length === 1 ? roles[0] : '';

  const [search, setSearch] = useState('');
  const [classIds, setClassIds] = useState<string[]>([]);
  const [page, setPage] = useState(1);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<User | null>(null);
  const [deleting, setDeleting] = useState<User | null>(null);

  const classes = useClassOptions();
  const remove = useDeleteUser();
  const query = useUsers({ search, role: roles, classId: classIds, page, pageSize: 10 });

  const items = query.data?.items ?? [];
  const heading = HEADINGS[role];
  // Name, email, role, actions — plus the identity columns that only make sense for one
  // role. On the unfiltered list a mixed table would leave most of them blank.
  const columnCount = role === 'Student' ? 6 : role === 'Teacher' ? 5 : 4;
  // The role is a heading here, not a filter the user needs telling about.
  const isFiltered = !!search || classIds.length > 0;
  const classOptions = (classes.data ?? []).map((c) => ({ value: c.id, label: c.name }));

  function withPageReset<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  // `replace`, not `push`: switching a dropdown should not stack a history entry the way
  // arriving from the sidebar does.
  function setRoles(next: string[]) {
    const params = new URLSearchParams(searchParams);
    params.delete('role');
    for (const value of next) {
      params.append('role', value);
    }
    const queryString = params.toString();
    router.replace(queryString ? `${pathname}?${queryString}` : pathname);
  }

  // A role change can arrive from the sidebar too, so paging resets on the value itself
  // rather than inside the dropdown's handler. Keyed on the joined list because the array
  // is rebuilt from the URL on every render and would never compare equal.
  const roleKey = roles.join(',');
  useEffect(() => {
    setPage(1);
  }, [roleKey]);

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        back={{ href: '/', label: 'Dashboard' }}
        eyebrow="Administration"
        title={heading.title}
        description={heading.description}
        actions={
          <Button onClick={openCreate}>
            <Plus className="size-4" />
            {heading.action}
          </Button>
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search name or email…"
            className="sm:max-w-xs"
          />
          <FilterSelect
            values={roles}
            onChange={setRoles}
            options={ROLE_OPTIONS}
            allLabel="All roles"
          />
          <FilterSelect
            values={classIds}
            onChange={withPageReset(setClassIds)}
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
                    {role === 'Student' && (
                      <>
                        <TableHead>Class</TableHead>
                        <TableHead>Student ID</TableHead>
                      </>
                    )}
                    {role === 'Teacher' && <TableHead>Teacher ID</TableHead>}
                    <TableHead className="w-20">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.isLoading ? (
                    <TableSkeleton columns={columnCount} />
                  ) : items.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={columnCount} className="p-0">
                        <EmptyState
                          icon={Users}
                          title={
                            isFiltered
                              ? 'Nothing matches those filters'
                              : `No ${heading.title.toLowerCase()} yet`
                          }
                          description={
                            isFiltered ? 'Try a different search term.' : 'Create the first account.'
                          }
                          action={
                            !isFiltered && (
                              <Button size="sm" onClick={openCreate}>
                                <Plus className="size-4" />
                                {heading.action}
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
                        {role === 'Student' && (
                          <>
                            <TableCell className="text-muted-foreground">
                              {user.classes.length > 0
                                ? user.classes.map((enrolled) => enrolled.className).join(', ')
                                : '—'}
                            </TableCell>
                            <TableCell className="font-mono text-sm text-muted-foreground">
                              {user.studentId ?? '—'}
                            </TableCell>
                          </>
                        )}
                        {role === 'Teacher' && (
                          <TableCell className="font-mono text-sm text-muted-foreground">
                            {user.teacherId ?? '—'}
                          </TableCell>
                        )}
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

      <UserFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        user={editing}
        defaultRole={role || undefined}
      />

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
