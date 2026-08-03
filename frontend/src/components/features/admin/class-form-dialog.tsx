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
import { useSaveClass } from '@/hooks/use-admin-resources';
import { classSchema, type ClassValues } from '@/schemas';
import type { ClassRoom } from '@/types/api';

export function ClassFormDialog({
  open,
  onOpenChange,
  classRoom,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  classRoom?: ClassRoom | null;
}) {
  const isEdit = !!classRoom;
  const save = useSaveClass();

  const form = useForm<ClassValues>({
    resolver: zodResolver(classSchema),
    defaultValues: { name: '', grade: '', section: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(
      classRoom
        ? { name: classRoom.name, grade: classRoom.grade ?? '', section: classRoom.section ?? '' }
        : { name: '', grade: '', section: '' },
    );
  }, [open, classRoom, form]);

  async function onSubmit(values: ClassValues) {
    await save.mutateAsync({
      id: classRoom?.id,
      input: { name: values.name, grade: values.grade || null, section: values.section || null },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit class' : 'Create class'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" placeholder="Grade 10 - Section A" {...form.register('name')} />
            {errors.name && <p className="text-xs text-danger">{errors.name.message}</p>}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="grade">Grade</Label>
              <Input id="grade" placeholder="10" {...form.register('grade')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="section">Section</Label>
              <Input id="section" placeholder="A" {...form.register('section')} />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create class'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
