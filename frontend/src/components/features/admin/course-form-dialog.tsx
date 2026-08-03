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
import { useSaveSubject } from '@/hooks/use-admin-resources';
import { subjectSchema, type SubjectValues } from '@/schemas';
import type { Subject } from '@/types/api';

export function SubjectFormDialog({
  open,
  onOpenChange,
  subject,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  subject?: Subject | null;
}) {
  const isEdit = !!subject;
  const save = useSaveSubject();

  const form = useForm<SubjectValues>({
    resolver: zodResolver(subjectSchema),
    defaultValues: { name: '', code: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(subject ? { name: subject.name, code: subject.code } : { name: '', code: '' });
  }, [open, subject, form]);

  async function onSubmit(values: SubjectValues) {
    await save.mutateAsync({
      id: subject?.id,
      input: { name: values.name, code: values.code.toUpperCase() },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit subject' : 'Create subject'}</DialogTitle>
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

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create subject'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
