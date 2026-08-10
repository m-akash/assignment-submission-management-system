'use client';

import { useEffect, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Eye, EyeOff } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Combobox } from '@/components/ui/combobox';
import { ClassPicker } from '@/components/shared/class-picker';
import { FormDrawer } from '@/components/shared/form-drawer';
import {
  useAcademicYearOptions,
  useClassOptions,
  useCurrentAcademicYear,
} from '@/hooks/use-admin-resources';
import { useSaveUser } from '@/hooks/use-admin-resources';
import { userSchema, type UserValues } from '@/schemas';
import type { Role, User } from '@/types/api';

const ROLES: Role[] = ['Admin', 'Teacher', 'Student'];

export function UserFormDrawer({
  open,
  onOpenChange,
  user,
  defaultRole = 'Student',
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  user?: User | null;
  /** Preselected role for a new account — the Teachers and Students screens open this
   *  drawer already scoped to the role the admin is looking at. */
  defaultRole?: Role;
}) {
  const isEdit = !!user;
  const [showPassword, setShowPassword] = useState(false);
  const classes = useClassOptions();
  const academicYears = useAcademicYearOptions();
  const currentAcademicYear = useCurrentAcademicYear();
  const save = useSaveUser();

  const emptyValues: UserValues = {
    fullName: '',
    email: '',
    role: defaultRole,
    classId: '',
    academicYearId: '',
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
            academicYearId: '',
            password: '',
            isEdit: true,
          }
        : { ...emptyValues, academicYearId: currentAcademicYear?.id ?? '' },
    );
    // currentAcademicYear is in the deps so a reset that ran before the options arrived is
    // redone once they do — otherwise opening the drawer on a cold cache leaves the year
    // blank and the admin has to pick the obvious answer by hand.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, user, defaultRole, currentAcademicYear?.id, form]);

  const role = form.watch('role');

  async function onSubmit(values: UserValues) {
    await save.mutateAsync({
      id: user?.id,
      input: {
        email: values.email,
        fullName: values.fullName,
        role: values.role,
        // Only meaningful on create, where they become the student's first enrollment.
        // Cleared for other roles so the server never sees a stale pairing.
        classId: values.role === 'Student' ? values.classId : null,
        academicYearId: values.role === 'Student' ? values.academicYearId : null,
        password: values.password || undefined,
      },
    });
    onOpenChange(false);
  }

  const errors = form.formState.errors;

  return (
    <FormDrawer
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? 'Edit user' : 'Create user'}
      description={
        isEdit
          ? 'Leave the password blank to keep the current one. Class membership is managed from the class roster.'
          : 'Accounts are created here by an administrator.'
      }
      submitLabel={isEdit ? 'Save changes' : 'Create user'}
      submitting={save.isPending}
      onSubmit={form.handleSubmit(onSubmit)}
    >
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
          <Combobox
            id="role"
            value={role}
            onChange={(value) => form.setValue('role', value as Role, { shouldValidate: true })}
            options={ROLES.map((r) => ({ value: r, label: r }))}
            disabled={isEdit}
          />
          {isEdit && <p className="text-xs text-muted-foreground">Role cannot be changed.</p>}
        </div>

      </div>

      {role === 'Student' && !isEdit && (
        <div className="space-y-1">
          <ClassPicker
            classes={classes.data ?? []}
            loading={classes.isLoading}
            value={form.watch('classId') ?? ''}
            onChange={(value) => form.setValue('classId', value, { shouldValidate: true })}
            invalid={!!errors.classId}
          />
          {errors.classId && <p className="text-xs text-danger">{errors.classId.message}</p>}
        </div>
      )}

      {role === 'Student' && !isEdit && (
        <div className="space-y-2">
          <Label htmlFor="academicYearId">Academic year</Label>
          <Combobox
            id="academicYearId"
            value={form.watch('academicYearId') ?? ''}
            onChange={(value) => form.setValue('academicYearId', value, { shouldValidate: true })}
            options={(academicYears.data ?? []).map((year) => ({
              value: year.id,
              label: year.name,
              hint: year.isCurrent ? 'Current' : undefined,
            }))}
            placeholder={
              academicYears.isLoading
                ? 'Loading…'
                : (academicYears.data ?? []).length === 0
                  ? 'No academic years yet — create one first'
                  : 'Choose the academic year'
            }
            searchPlaceholder="Search academic years…"
            emptyMessage="No academic years match"
            aria-invalid={!!errors.academicYearId}
            clearable
          />
          <p className="text-xs text-muted-foreground">
            The session this enrollment belongs to. Defaults to the current year.
          </p>
          {errors.academicYearId && (
            <p className="text-xs text-danger">{errors.academicYearId.message}</p>
          )}
        </div>
      )}

      <div className="space-y-2">
        <Label htmlFor="password">{isEdit ? 'New password' : 'Password'}</Label>
        <div className="relative">
          <Input
            id="password"
            type={showPassword ? 'text' : 'password'}
            placeholder={isEdit ? 'Leave blank to keep current' : 'At least 8 characters'}
            className="pr-10"
            {...form.register('password')}
          />
          <button
            type="button"
            onClick={() => setShowPassword((visible) => !visible)}
            aria-label={showPassword ? 'Hide password' : 'Show password'}
            className="absolute top-1/2 right-1.5 flex size-8 -translate-y-1/2 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
          >
            {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        </div>
        {errors.password && <p className="text-xs text-danger">{errors.password.message}</p>}
      </div>
    </FormDrawer>
  );
}
