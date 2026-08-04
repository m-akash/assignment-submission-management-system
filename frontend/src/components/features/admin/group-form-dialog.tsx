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
import { useSaveGroup } from '@/hooks/use-admin-resources';
import { groupSchema, type GroupValues } from '@/schemas';
import type { Group } from '@/types/api';

export function GroupFormDialog({
  open,
  onOpenChange,
  group,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  group?: Group | null;
}) {
  const isEdit = !!group;
  const save = useSaveGroup();

  const form = useForm<GroupValues>({
    resolver: zodResolver(groupSchema),
    defaultValues: { name: '', code: '' },
  });

  useEffect(() => {
    if (!open) return;
    form.reset(group ? { name: group.name, code: group.code } : { name: '', code: '' });
  }, [open, group, form]);

  async function onSubmit(values: GroupValues) {
    await save.mutateAsync({
      id: group?.id,
      input: { name: values.name, code: values.code.toUpperCase() },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit group' : 'Create group'}</DialogTitle>
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
              Short code for the stream, e.g. SCI.
            </p>
            {errors.code && <p className="text-xs text-danger">{errors.code.message}</p>}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create group'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
