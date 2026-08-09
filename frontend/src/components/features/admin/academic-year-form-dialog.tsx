'use client';

import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useSaveAcademicYear } from '@/hooks/use-admin-resources';
import { academicYearSchema, type AcademicYearValues } from '@/schemas';
import type { AcademicYear } from '@/types/api';

export function AcademicYearFormDialog({
  open,
  onOpenChange,
  academicYear,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  academicYear?: AcademicYear | null;
}) {
  const isEdit = !!academicYear;
  const save = useSaveAcademicYear();

  const form = useForm<AcademicYearValues>({
    resolver: zodResolver(academicYearSchema),
    defaultValues: { name: '', startDate: '', endDate: '', isCurrent: false },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(
      academicYear
        ? {
            name: academicYear.name,
            // Already "YYYY-MM-DD" from the API, which is exactly what a date input wants —
            // no parsing, so no chance of the day moving with the reader's time zone.
            startDate: academicYear.startDate,
            endDate: academicYear.endDate,
            isCurrent: academicYear.isCurrent,
          }
        : { name: '', startDate: '', endDate: '', isCurrent: false },
    );
  }, [open, academicYear, form]);

  async function onSubmit(values: AcademicYearValues) {
    await save.mutateAsync({ id: academicYear?.id, input: values });
    onOpenChange(false);
  }

  const errors = form.formState.errors;
  const isCurrent = form.watch('isCurrent');
  // Only worth warning about when it would actually move the flag off another year.
  const willTakeCurrent = isCurrent && !academicYear?.isCurrent;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit academic year' : 'Create academic year'}</DialogTitle>
          <DialogDescription>
            A session students are enrolled into. Enrollments name the year they belong to, so
            the same student can sit in Class IX one year and Class X the next.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" placeholder="2026-2027" {...form.register('name')} />
            <p className="text-xs text-muted-foreground">
              Whatever the school calls the session.
            </p>
            {errors.name && <p className="text-xs text-danger">{errors.name.message}</p>}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="startDate">Starts</Label>
              <Input id="startDate" type="date" {...form.register('startDate')} />
              {errors.startDate && (
                <p className="text-xs text-danger">{errors.startDate.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="endDate">Ends</Label>
              <Input id="endDate" type="date" {...form.register('endDate')} />
              {errors.endDate && <p className="text-xs text-danger">{errors.endDate.message}</p>}
            </div>
          </div>

          <div className="flex items-start gap-3 rounded-md border p-3">
            <Checkbox
              id="isCurrent"
              checked={isCurrent}
              onCheckedChange={(checked) =>
                form.setValue('isCurrent', checked === true, { shouldValidate: true })
              }
              className="mt-0.5"
            />
            <div className="space-y-1">
              <Label htmlFor="isCurrent" className="font-medium">
                This is the current session
              </Label>
              <p className="text-xs text-muted-foreground">
                {willTakeCurrent
                  ? 'Whichever year holds this now will lose it. Enrollment forms open on the current year.'
                  : 'Enrollment forms open on the current year.'}
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create academic year'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
