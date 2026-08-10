'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Label } from '@/components/ui/label';
import { Combobox } from '@/components/ui/combobox';
import { ClassPicker } from '@/components/shared/class-picker';
import { FormDrawer } from '@/components/shared/form-drawer';
import {
  useClassOptions,
  useCourseOptions,
  useCreateClassCourse,
} from '@/hooks/use-admin-resources';
import { classCourseSchema, type ClassCourseValues } from '@/schemas';

/**
 * Adds a course to a class — the offering everything else is scoped to.
 *
 * Create-only, with no edit: changing which class or course an offering points at would
 * silently move every assignment and submission underneath it. Remove it and add the right
 * one instead, which the server refuses while anything still depends on it.
 */
export function ClassCourseFormDrawer({
  open,
  onOpenChange,
  /** Preselected when opened from a specific class's row. */
  defaultClassId,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  defaultClassId?: string;
}) {
  const classes = useClassOptions();
  const courses = useCourseOptions();
  const create = useCreateClassCourse();

  const form = useForm<ClassCourseValues>({
    resolver: zodResolver(classCourseSchema),
    defaultValues: { classId: defaultClassId ?? '', courseId: '' },
  });

  useEffect(() => {
    if (open) form.reset({ classId: defaultClassId ?? '', courseId: '' });
  }, [open, defaultClassId, form]);

  async function onSubmit(values: ClassCourseValues) {
    await create.mutateAsync(values);
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <FormDrawer
      open={open}
      onOpenChange={onOpenChange}
      title="Add a course to a class"
      description="Records that this class studies this course. Teachers can then be assigned to it, and assignments created against it."
      submitLabel="Add course"
      submitting={create.isPending}
      onSubmit={form.handleSubmit(onSubmit)}
      width="sm"
    >
      <div className="space-y-1">
        <ClassPicker
          classes={classes.data ?? []}
          loading={classes.isLoading}
          value={form.watch('classId')}
          onChange={(value) => form.setValue('classId', value, { shouldValidate: true })}
          invalid={!!errors.classId}
        />
        {errors.classId && <p className="text-xs text-danger">{errors.classId.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="courseId">Course</Label>
        <Combobox
          id="courseId"
          value={form.watch('courseId')}
          onChange={(value) => form.setValue('courseId', value, { shouldValidate: true })}
          // The code is a hint rather than part of the label so that searching for it
          // works without the option list reading as a wall of parenthesised codes.
          options={(courses.data ?? []).map((course) => ({
            value: course.id,
            label: course.name,
            hint: course.code,
          }))}
          placeholder={courses.isLoading ? 'Loading…' : 'Choose a course'}
          searchPlaceholder="Search name or code…"
          emptyMessage="No courses match"
          aria-invalid={!!errors.courseId}
          clearable
        />
        {errors.courseId && <p className="text-xs text-danger">{errors.courseId.message}</p>}
      </div>
    </FormDrawer>
  );
}
