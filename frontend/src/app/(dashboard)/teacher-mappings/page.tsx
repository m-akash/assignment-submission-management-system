'use client';

import { useState } from 'react';
import { Link2, Plus, Trash2 } from 'lucide-react';
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
import { TeacherMappingFormDrawer } from '@/components/features/admin/teacher-mapping-form-drawer';
import {
  useClassOptions,
  useDeleteTeacherMapping,
  useCourseOptions,
  useTeacherMappings,
  useUsers,
} from '@/hooks/use-admin-resources';
import { classLabel, gradeLabel, sectionLabel } from '@/lib/format';
import type { TeacherMapping } from '@/types/api';

export default function TeacherMappingsPage() {
  return (
    <RoleGuard allow={['Admin']}>
      <MappingsView />
    </RoleGuard>
  );
}

function MappingsView() {
  const [search, setSearch] = useState('');
  const [teacherIds, setTeacherIds] = useState<string[]>([]);
  const [courseIds, setCourseIds] = useState<string[]>([]);
  const [classIds, setClassIds] = useState<string[]>([]);
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [deleting, setDeleting] = useState<TeacherMapping | null>(null);

  const teachers = useUsers({ role: 'Teacher', pageSize: 100 });
  const courses = useCourseOptions();
  const classes = useClassOptions();
  const remove = useDeleteTeacherMapping();
  const query = useTeacherMappings({
    search,
    teacherId: teacherIds,
    courseId: courseIds,
    classId: classIds,
    page,
    pageSize: 10,
  });
  const items = query.data?.items ?? [];
  const isFiltered =
    !!search || teacherIds.length > 0 || courseIds.length > 0 || classIds.length > 0;
  // Every teacher, not only the mapped ones — an unmapped teacher is a legitimate thing to
  // filter by, and its empty result is the answer.
  const teacherOptions = (teachers.data?.items ?? []).map((teacher) => ({
    value: teacher.id,
    label: teacher.fullName,
  }));
  const courseOptions = (courses.data ?? []).map((s) => ({ value: s.id, label: s.name }));

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
        title="Teaching assignments"
        description="Each row authorises one teacher to create assignments for one class and course."
        actions={
          <Button onClick={() => setFormOpen(true)}>
            <Plus className="size-4" />
            Assign teacher
          </Button>
        }
      />

      <div className="panel overflow-hidden">
        <div className="flex flex-col gap-2 border-b p-4 sm:flex-row sm:flex-wrap">
          <SearchInput
            value={search}
            onChange={withPageReset(setSearch)}
            placeholder="Search teacher, course, grade or section…"
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
                    <TableHead>Teacher</TableHead>
                    <TableHead>Course</TableHead>
                    <TableHead>Code</TableHead>
                    <TableHead>Class</TableHead>
                    <TableHead>Section</TableHead>
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
                          icon={Link2}
                          title={isFiltered ? 'Nothing matches those filters' : 'No teaching assignments yet'}
                          description={
                            isFiltered
                              ? 'Try a different search term or filter.'
                              : 'Assign a teacher to a class and course so they can create work.'
                          }
                          action={
                            !isFiltered && (
                              <Button size="sm" onClick={() => setFormOpen(true)}>
                                <Plus className="size-4" />
                                Assign teacher
                              </Button>
                            )
                          }
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    items.map((mapping) => (
                      <TableRow key={mapping.id}>
                        <TableCell>
                          <p className="font-medium">{mapping.teacherName}</p>
                          <p className="text-xs text-muted-foreground">{mapping.teacherEmail}</p>
                        </TableCell>
                        <TableCell>{mapping.courseName}</TableCell>
                        <TableCell className="font-mono text-xs text-muted-foreground">
                          {mapping.courseCode}
                        </TableCell>
                        <TableCell>{gradeLabel(mapping.classLevel)}</TableCell>
                        <TableCell className="text-muted-foreground">
                          {sectionLabel(mapping.classSection)}
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove ${mapping.teacherName} from ${classLabel(mapping.classLevel, mapping.classSection)}`}
                            disabled={remove.isPending}
                            onClick={() => setDeleting(mapping)}
                          >
                            <Trash2 className="size-4 text-danger" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>

            {query.data && (
              <PaginationBar pagination={query.data.pagination} onPageChange={setPage} itemLabel="assignments" />
            )}
          </>
        )}
      </div>

      <TeacherMappingFormDrawer open={formOpen} onOpenChange={setFormOpen} />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Remove this teaching assignment?"
        description={
          deleting
            ? `${deleting.teacherName} will no longer be able to create assignments for ${classLabel(deleting.classLevel, deleting.classSection)} · ${deleting.courseName}.`
            : ''
        }
        confirmLabel="Remove"
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
