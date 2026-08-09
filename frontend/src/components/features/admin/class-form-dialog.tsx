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
import { classSchema, type ClassInput, type ClassValues } from '@/schemas';
import type { ClassRoom } from '@/types/api';

const ROMAN = ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI', 'XII'];

/**
 * Mirrors the name the server composes, so the admin can see what they are about to create
 * even though the name is no longer a field. Returns null while the inputs are incomplete.
 */
function previewName(level: unknown, section: string): string | null {
  const grade = Number(level);
  const trimmed = section.trim();
  if (!Number.isInteger(grade) || grade < 1 || grade > 12 || !trimmed) return null;
  return `Class ${ROMAN[grade - 1]} - Section ${trimmed}`;
}

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

  // <what the fields hold, context, what validation produces> — `level` is coerced,
  // so the first and last are not the same type.
  const form = useForm<ClassInput, unknown, ClassValues>({
    resolver: zodResolver(classSchema),
    defaultValues: { level: 6, section: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(
      classRoom
        ? { level: classRoom.level, section: classRoom.section ?? '' }
        : { level: 6, section: '' },
    );
  }, [open, classRoom, form]);

  async function onSubmit(values: ClassValues) {
    await save.mutateAsync({
      id: classRoom?.id,
      input: { level: values.level, section: values.section },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;
  const preview = previewName(form.watch('level'), form.watch('section') ?? '');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit class' : 'Create class'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="level">Grade</Label>
              <Input id="level" type="number" min={1} max={12} placeholder="10" {...form.register('level')} />
              <p className="text-xs text-muted-foreground">1–12. Shown as a Roman numeral.</p>
              {errors.level && <p className="text-xs text-danger">{errors.level.message}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="section">Section</Label>
              <Input id="section" placeholder="A" {...form.register('section')} />
              <p className="text-xs text-muted-foreground">One class per grade and section.</p>
              {errors.section && <p className="text-xs text-danger">{errors.section.message}</p>}
            </div>
          </div>

          {/* The name is composed server-side, so show what it will be rather than asking for it. */}
          <p className="text-xs text-muted-foreground">
            Name: <span className="font-medium text-foreground">{preview ?? '—'}</span>
          </p>

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
