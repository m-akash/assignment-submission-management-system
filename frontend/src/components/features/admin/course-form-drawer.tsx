'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { FormDrawer } from '@/components/shared/form-drawer';
import { useSaveCourse } from '@/hooks/use-admin-resources';
import { courseSchema, type CourseValues } from '@/schemas';
import type { Course } from '@/types/api';

export function CourseFormDrawer({
  open,
  onOpenChange,
  course,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  course?: Course | null;
}) {
  const isEdit = !!course;
  const save = useSaveCourse();

  const form = useForm<CourseValues>({
    resolver: zodResolver(courseSchema),
    defaultValues: { name: '', code: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(course ? { name: course.name, code: course.code } : { name: '', code: '' });
  }, [open, course, form]);

  async function onSubmit(values: CourseValues) {
    await save.mutateAsync({
      id: course?.id,
      input: {
        name: values.name,
        code: values.code.toUpperCase(),
      },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <FormDrawer
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? 'Edit course' : 'Create course'}
      submitLabel={isEdit ? 'Save changes' : 'Create course'}
      submitting={save.isPending}
      onSubmit={form.handleSubmit(onSubmit)}
    >
      <div className="space-y-2">
        <Label htmlFor="name">Name</Label>
        <Input id="name" placeholder="Mathematics" {...form.register('name')} />
        {errors.name && <p className="text-xs text-danger">{errors.name.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="code">Code</Label>
        <Input id="code" placeholder="MATH101" className="uppercase" {...form.register('code')} />
        {errors.code && <p className="text-xs text-danger">{errors.code.message}</p>}
      </div>
    </FormDrawer>
  );
}
