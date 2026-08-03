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
import { useSaveDepartment } from '@/hooks/use-admin-resources';
import { departmentSchema, type DepartmentValues } from '@/schemas';
import type { Department } from '@/types/api';

export function DepartmentFormDialog({
  open,
  onOpenChange,
  department,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  department?: Department | null;
}) {
  const isEdit = !!department;
  const save = useSaveDepartment();

  const form = useForm<DepartmentValues>({
    resolver: zodResolver(departmentSchema),
    defaultValues: { name: '', code: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(department ? { name: department.name, code: department.code } : { name: '', code: '' });
  }, [open, department, form]);

  async function onSubmit(values: DepartmentValues) {
    await save.mutateAsync({
      id: department?.id,
      input: { name: values.name, code: values.code.toUpperCase() },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit department' : 'Create department'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" placeholder="Science" {...form.register('name')} />
            {errors.name && <p className="text-xs text-danger">{errors.name.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="code">Code</Label>
            <Input id="code" placeholder="SCI" className="uppercase" {...form.register('code')} />
            <p className="text-xs text-muted-foreground">
              Used to build teacher ids, e.g. <span className="font-mono">INS-SCI-01</span>.
            </p>
            {errors.code && <p className="text-xs text-danger">{errors.code.message}</p>}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create department'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
