'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Combobox } from '@/components/ui/combobox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { useSaveClass } from '@/hooks/use-admin-resources';
import { GRADE_CHOICES, SECTION_CHOICES } from '@/lib/classes';
import { gradeLabel } from '@/lib/format';
import { classSchema, type ClassInput, type ClassValues } from '@/schemas';
import type { ClassRoom } from '@/types/api';

const DEFAULT_GRADE = 6;

/**
 * Both fields are chosen, never typed: a grade is one of the seven the school runs and a
 * section is a letter, so a free-text box could only produce cohorts that read differently
 * from every other one ("9" vs "IX", "a" vs "A").
 *
 * A value already on the class that is not in the standard list is kept as an extra option
 * rather than dropped — editing a class must never quietly move it somewhere else.
 */
function withCurrent<T extends string | number>(choices: readonly T[], current: T | null | undefined) {
  return current !== null && current !== undefined && current !== ('' as T) && !choices.includes(current)
    ? [current, ...choices]
    : [...choices];
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
    defaultValues: { level: DEFAULT_GRADE, section: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(
      classRoom
        ? { level: classRoom.level, section: classRoom.section ?? '' }
        : { level: DEFAULT_GRADE, section: '' },
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
  const level = form.watch('level');
  const section = form.watch('section') ?? '';

  const gradeOptions = withCurrent(GRADE_CHOICES, classRoom?.level);
  const sectionOptions = withCurrent(SECTION_CHOICES, classRoom?.section);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit class' : 'Create class'}</DialogTitle>
          <DialogDescription>
            A class is a grade and a section. One class per pair — a grade can hold as many
            sections as it needs.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="level">Grade</Label>
              <Combobox
                id="level"
                value={level === undefined || level === null ? '' : String(level)}
                onChange={(value) =>
                  form.setValue('level', Number(value), { shouldValidate: true })
                }
                options={gradeOptions.map((grade) => ({
                  value: String(grade),
                  label: gradeLabel(Number(grade)),
                }))}
                placeholder="Choose a grade"
                searchPlaceholder="Search grades…"
                emptyMessage="No grades match"
                aria-invalid={!!errors.level}
              />
              {errors.level && <p className="text-xs text-danger">{errors.level.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="section">Section</Label>
              <Combobox
                id="section"
                value={section}
                onChange={(value) => form.setValue('section', value, { shouldValidate: true })}
                options={sectionOptions.map((letter) => ({
                  value: String(letter),
                  label: String(letter),
                }))}
                placeholder="Choose a section"
                // A–D are the first rows of the list; E–Z are found by typing rather than
                // by scrolling through twenty-two letters nobody uses.
                searchPlaceholder="A–Z — type to jump"
                emptyMessage="No sections match"
                aria-invalid={!!errors.section}
              />
              {errors.section && <p className="text-xs text-danger">{errors.section.message}</p>}
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
