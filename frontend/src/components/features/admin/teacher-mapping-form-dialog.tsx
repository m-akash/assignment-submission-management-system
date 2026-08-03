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
  useCreateTeacherMapping,
  useSubjectOptions,
  useUsers,
} from '@/hooks/use-admin-resources';
import { teacherMappingSchema, type TeacherMappingValues } from '@/schemas';

export function TeacherMappingFormDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const teachers = useUsers({ role: 'Teacher', pageSize: 100 });
  const classes = useClassOptions();
  const subjects = useSubjectOptions();
  const create = useCreateTeacherMapping();

  const form = useForm<TeacherMappingValues>({
    resolver: zodResolver(teacherMappingSchema),
    defaultValues: { teacherId: '', subjectId: '', classId: '' },
  });

  useEffect(() => {
    if (open) form.reset({ teacherId: '', subjectId: '', classId: '' });
  }, [open, form]);

  async function onSubmit(values: TeacherMappingValues) {
    await create.mutateAsync(values);
    onOpenChange(false);
  }

  const teacherOptions = teachers.data?.items ?? [];
  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>Assign a teacher</DialogTitle>
          <DialogDescription>
            Links one teacher to one subject and class. This is what lets that teacher create
            assignments for that class.
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
            <Label htmlFor="subjectId">Subject</Label>
            <Select
              value={form.watch('subjectId')}
              onValueChange={(value) => form.setValue('subjectId', value, { shouldValidate: true })}
            >
              <SelectTrigger id="subjectId">
                <SelectValue placeholder={subjects.isLoading ? 'Loading…' : 'Choose a subject'} />
              </SelectTrigger>
              <SelectContent>
                {(subjects.data ?? []).map((subject) => (
                  <SelectItem key={subject.id} value={subject.id}>
                    {subject.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.subjectId && <p className="text-xs text-danger">{errors.subjectId.message}</p>}
          </div>

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
