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
export function TeacherMappingFormDialog({
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
  const offeringOptions = offerings.data ?? [];
  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>Assign a teacher</DialogTitle>
          <DialogDescription>
            Links one teacher to one course offering. This is what lets that teacher create
            assignments and grade submissions for that class and course.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="teacherId">Teacher</Label>
            <Select
              value={form.watch('teacherId')}
              onValueChange={(value) => form.setValue('teacherId', value, { shouldValidate: true })}
            >
              <SelectTrigger id="teacherId">
                <SelectValue placeholder={teachers.isLoading ? 'Loading…' : 'Choose a teacher'} />
              </SelectTrigger>
              <SelectContent>
                {teacherOptions.map((teacher) => (
                  <SelectItem key={teacher.id} value={teacher.id}>
                    {teacher.fullName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.teacherId && <p className="text-xs text-danger">{errors.teacherId.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="classCourseId">Class and course</Label>
            <Select
              value={form.watch('classCourseId')}
              onValueChange={(value) =>
                form.setValue('classCourseId', value, { shouldValidate: true })
              }
            >
              <SelectTrigger id="classCourseId">
                <SelectValue
                  placeholder={offerings.isLoading ? 'Loading…' : 'Choose a class and course'}
                />
              </SelectTrigger>
              <SelectContent>
                {offeringOptions.map((offering) => (
                  <SelectItem key={offering.id} value={offering.id}>
                    {offering.className} · {offering.courseName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.classCourseId && (
              <p className="text-xs text-danger">{errors.classCourseId.message}</p>
            )}
            {!offerings.isLoading && offeringOptions.length === 0 && (
              <p className="text-xs text-muted-foreground">
                No offerings yet — add a course to a class on the Offerings screen first.
              </p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Loader2 className="size-4 animate-spin" />}
              Assign
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
