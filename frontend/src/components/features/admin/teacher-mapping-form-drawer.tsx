'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Label } from '@/components/ui/label';
import { Combobox } from '@/components/ui/combobox';
import { FormDrawer } from '@/components/shared/form-drawer';
import { classLabel } from '@/lib/format';
import {
  useClassCourseOptions,
  useCreateTeacherMapping,
  useUsers,
} from '@/hooks/use-admin-resources';
import { teacherMappingSchema, type TeacherMappingValues } from '@/schemas';

const EMPTY: TeacherMappingValues = { teacherId: '', classCourseId: '' };

/**
 * Assigns a teacher to a course offering.
 *
 * One picker for the offering rather than separate class and course pickers: the admin can
 * then only choose a combination the class actually studies, which is the point of the
 * offering existing. Creating a new pairing is a different job, done on the Offerings screen.
 */
export function TeacherMappingFormDrawer({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const teachers = useUsers({ role: 'Teacher', pageSize: 100 });
  const offerings = useClassCourseOptions();
  const create = useCreateTeacherMapping();

  const form = useForm<TeacherMappingValues>({
    resolver: zodResolver(teacherMappingSchema),
    defaultValues: EMPTY,
  });

  useEffect(() => {
    if (open) form.reset(EMPTY);
  }, [open, form]);

  async function onSubmit(values: TeacherMappingValues) {
    await create.mutateAsync(values);
    onOpenChange(false);
  }

  const teacherOptions = teachers.data?.items ?? [];
  // Each offering may have at most one teacher, so one already carrying a mapping isn't a
  // valid target here — it has to be removed on the mappings screen before it can take
  // another teacher.
  const offeringOptions = (offerings.data ?? []).filter((offering) => offering.teacherCount === 0);
  const errors = form.formState.errors;

  return (
    <FormDrawer
      open={open}
      onOpenChange={onOpenChange}
      title="Assign a teacher"
      description="Links one teacher to one course offering. This is what lets that teacher create assignments and grade submissions for that class and course."
      submitLabel="Assign"
      submitting={create.isPending}
      onSubmit={form.handleSubmit(onSubmit)}
      width="sm"
    >
      <div className="space-y-2">
        <Label htmlFor="teacherId">Teacher</Label>
        <Combobox
          id="teacherId"
          value={form.watch('teacherId')}
          onChange={(value) => form.setValue('teacherId', value, { shouldValidate: true })}
          options={teacherOptions.map((teacher) => ({
            value: teacher.id,
            label: teacher.fullName,
            hint: teacher.email,
          }))}
          placeholder={teachers.isLoading ? 'Loading…' : 'Choose a teacher'}
          searchPlaceholder="Search name or email…"
          emptyMessage="No teachers match"
          aria-invalid={!!errors.teacherId}
          clearable
        />
        {errors.teacherId && <p className="text-xs text-danger">{errors.teacherId.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="classCourseId">Class and course</Label>
        <Combobox
          id="classCourseId"
          value={form.watch('classCourseId')}
          onChange={(value) => form.setValue('classCourseId', value, { shouldValidate: true })}
          options={offeringOptions.map((offering) => ({
            value: offering.id,
            label: `${classLabel(offering.classLevel, offering.classSection)} · ${offering.courseName}`,
          }))}
          placeholder={offerings.isLoading ? 'Loading…' : 'Choose a class and course'}
          searchPlaceholder="Search grade, section or course…"
          emptyMessage="No offerings match"
          aria-invalid={!!errors.classCourseId}
          clearable
        />
        {errors.classCourseId && (
          <p className="text-xs text-danger">{errors.classCourseId.message}</p>
        )}
        {!offerings.isLoading && offeringOptions.length === 0 && (
          <p className="text-xs text-muted-foreground">
            {(offerings.data ?? []).length === 0
              ? 'No offerings yet — add a course to a class on the Offerings screen first.'
              : 'Every offering already has a teacher. Remove a mapping on this screen to free one up.'}
          </p>
        )}
      </div>
    </FormDrawer>
  );
}
