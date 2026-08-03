'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useDepartmentOptions, useSaveCourse } from '@/hooks/use-admin-resources';
import { courseSchema, type CourseValues } from '@/schemas';
import type { Course } from '@/types/api';

export function CourseFormDialog({
  open,
  onOpenChange,
  course,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  course?: Course | null;
}) {
  const isEdit = !!course;
  const departments = useDepartmentOptions();
  const save = useSaveCourse();

  const form = useForm<CourseValues>({
    resolver: zodResolver(courseSchema),
    defaultValues: { name: '', code: '', departmentId: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(
      course
        ? { name: course.name, code: course.code, departmentId: course.departmentId }
        : { name: '', code: '', departmentId: '' },
    );
  }, [open, course, form]);

  async function onSubmit(values: CourseValues) {
    await save.mutateAsync({
      id: course?.id,
      input: {
        name: values.name,
        code: values.code.toUpperCase(),
        departmentId: values.departmentId,
      },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit course' : 'Create course'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
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

          <div className="space-y-2">
            <Label htmlFor="departmentId">Department</Label>
            <Select
              value={form.watch('departmentId')}
              onValueChange={(value) => form.setValue('departmentId', value, { shouldValidate: true })}
            >
              <SelectTrigger id="departmentId">
                <SelectValue placeholder="Choose a department" />
              </SelectTrigger>
              <SelectContent>
                {(departments.data ?? []).map((d) => (
                  <SelectItem key={d.id} value={d.id}>
                    {d.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.departmentId && (
              <p className="text-xs text-danger">{errors.departmentId.message}</p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create course'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
