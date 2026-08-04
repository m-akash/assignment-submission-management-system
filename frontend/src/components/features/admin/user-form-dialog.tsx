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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useClassOptions } from '@/hooks/use-admin-resources';
import { useSaveUser } from '@/hooks/use-admin-resources';
import { userSchema, type UserValues } from '@/schemas';
import type { Role, User } from '@/types/api';

const ROLES: Role[] = ['Admin', 'Teacher', 'Student'];

export function UserFormDialog({
  open,
  onOpenChange,
  user,
  defaultRole = 'Student',
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  user?: User | null;
  /** Preselected role for a new account — the Teachers and Students screens open this
   *  dialog already scoped to the role the admin is looking at. */
  defaultRole?: Role;
}) {
  const isEdit = !!user;
  const classes = useClassOptions();
  const save = useSaveUser();

  const emptyValues: UserValues = {
    fullName: '',
    email: '',
    role: defaultRole,
    classId: '',
    password: '',
    isEdit: false,
  };

  const form = useForm<UserValues>({
    resolver: zodResolver(userSchema),
    defaultValues: emptyValues,
  });

  useEffect(() => {
    if (!open) return;
    form.reset(
      user
        ? {
            fullName: user.fullName,
            email: user.email,
            role: user.role,
            // Create-only: an existing student's classes are managed on the class roster,
            // where removing their last one can be refused.
            classId: '',
            password: '',
            isEdit: true,
          }
        : { ...emptyValues },
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, user, defaultRole, form]);

  const role = form.watch('role');

  async function onSubmit(values: UserValues) {
    await save.mutateAsync({
      id: user?.id,
      input: {
        email: values.email,
        fullName: values.fullName,
        role: values.role,
        // Only meaningful on create, where it becomes the student's first enrollment.
        // Cleared for other roles so the server never sees a stale pairing.
        classId: values.role === 'Student' ? values.classId : null,
        password: values.password || undefined,
      },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit user' : 'Create user'}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? 'Leave the password blank to keep the current one. Class membership is managed from the class roster.'
              : 'Self-registration is disabled — accounts are created here by an administrator.'}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="fullName">Full name</Label>
            <Input id="fullName" {...form.register('fullName')} />
            {errors.fullName && <p className="text-xs text-danger">{errors.fullName.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" disabled={isEdit} {...form.register('email')} />
            {isEdit && <p className="text-xs text-muted-foreground">Email cannot be changed.</p>}
            {errors.email && <p className="text-xs text-danger">{errors.email.message}</p>}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="role">Role</Label>
              <Select
                value={role}
                onValueChange={(value) => form.setValue('role', value as Role, { shouldValidate: true })}
                disabled={isEdit}
              >
                <SelectTrigger id="role">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ROLES.map((r) => (
                    <SelectItem key={r} value={r}>
                      {r}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {isEdit && <p className="text-xs text-muted-foreground">Role cannot be changed.</p>}
            </div>

            {role === 'Student' && !isEdit && (
              <div className="space-y-2">
                <Label htmlFor="classId">Class</Label>
                <Select
                  value={form.watch('classId')}
                  onValueChange={(value) => form.setValue('classId', value, { shouldValidate: true })}
                >
                  <SelectTrigger id="classId">
                    <SelectValue placeholder="Choose a class" />
                  </SelectTrigger>
                  <SelectContent>
                    {(classes.data ?? []).map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errors.classId && <p className="text-xs text-danger">{errors.classId.message}</p>}
              </div>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="password">{isEdit ? 'New password' : 'Password'}</Label>
            <Input
              id="password"
              type="password"
              placeholder={isEdit ? 'Leave blank to keep current' : 'At least 8 characters'}
              {...form.register('password')}
            />
            {errors.password && <p className="text-xs text-danger">{errors.password.message}</p>}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isEdit ? 'Save changes' : 'Create user'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
