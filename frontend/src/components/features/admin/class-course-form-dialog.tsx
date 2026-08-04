'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
export function ClassCourseFormDialog({
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
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>Add a course to a class</DialogTitle>
          <DialogDescription>
            Records that this class studies this course. Teachers can then be assigned to it,
            and assignments created against it.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="classId">Class</Label>
            <Select
              value={form.watch('classId')}
              onValueChange={(value) => form.setValue('classId', value, { shouldValidate: true })}
            >
              <SelectTrigger id="classId">
                <SelectValue placeholder={classes.isLoading ? 'Loading…' : 'Choose a class'} />
              </SelectTrigger>
              <SelectContent>
                {(classes.data ?? []).map((classRoom) => (
                  <SelectItem key={classRoom.id} value={classRoom.id}>
                    {classRoom.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.classId && <p className="text-xs text-danger">{errors.classId.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="courseId">Course</Label>
            <Select
              value={form.watch('courseId')}
              onValueChange={(value) => form.setValue('courseId', value, { shouldValidate: true })}
            >
              <SelectTrigger id="courseId">
                <SelectValue placeholder={courses.isLoading ? 'Loading…' : 'Choose a course'} />
              </SelectTrigger>
              <SelectContent>
                {(courses.data ?? []).map((course) => (
                  <SelectItem key={course.id} value={course.id}>
                    {course.name} ({course.code})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.courseId && <p className="text-xs text-danger">{errors.courseId.message}</p>}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Loader2 className="size-4 animate-spin" />}
              Add course
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
