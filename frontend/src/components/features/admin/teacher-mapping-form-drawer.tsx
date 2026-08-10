'use client';

import { useEffect, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Label } from '@/components/ui/label';
import { Combobox } from '@/components/ui/combobox';
import { ClassPicker } from '@/components/shared/class-picker';
import { FormDrawer } from '@/components/shared/form-drawer';
import {
  useClassCourseOptions,
  useClassOptions,
  useCreateTeacherMapping,
  useUsers,
} from '@/hooks/use-admin-resources';
import { teacherMappingSchema, type TeacherMappingValues } from '@/schemas';

const EMPTY: TeacherMappingValues = { teacherId: '', classCourseId: '' };

/**
 * Assigns a teacher to a course offering.
 *
 * Narrowed in three steps — class, then section, then course — because the offering
 * catalogue is the product of the two, and a single list of every one of them is unreadable
 * by the time a school has a dozen classes. Each box is populated off the one before it, so
 * only combinations that exist can be expressed: the sections are the ones that grade has,
 * and the courses are the ones that cohort actually studies. Creating a new class/course
 * pairing is a different job, done on the Offerings screen.
 *
 * The submitted value is still the single offering id the API wants; the three dropdowns are
 * only how it is arrived at.
 */
export function TeacherMappingFormDrawer({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const teachers = useUsers({ role: 'Teacher', pageSize: 100 });
  const offerings = useClassCourseOptions();
  const classes = useClassOptions();
  const create = useCreateTeacherMapping();

  // Only consulted between picking a class and picking a course — once a course is chosen it
  // is the offering that says which class this is, so the two cannot drift apart and a
  // reopened drawer needs no synchronising.
  const [pendingClassId, setPendingClassId] = useState('');

  const form = useForm<TeacherMappingValues>({
    resolver: zodResolver(teacherMappingSchema),
    defaultValues: EMPTY,
  });

  useEffect(() => {
    if (open) {
      form.reset(EMPTY);
      setPendingClassId('');
    }
  }, [open, form]);

  async function onSubmit(values: TeacherMappingValues) {
    await create.mutateAsync(values);
    onOpenChange(false);
  }

  const teacherOptions = teachers.data?.items ?? [];
  // Each offering may have at most one teacher, so one already carrying a mapping isn't a
  // valid target here — it has to be removed on the mappings screen before it can take
  // another teacher.
  const offeringOptions = (offerings.data ?? []).filter((offering) => offering.teacherCount === 0);

  const classCourseId = form.watch('classCourseId');
  const selectedOffering = offeringOptions.find((offering) => offering.id === classCourseId);
  const classId = selectedOffering?.classId ?? pendingClassId;

  // A class with nothing left to assign would be a dead end in the first two boxes, so it
  // never reaches them.
  const classOptions = (classes.data ?? []).filter((classRoom) =>
    offeringOptions.some((offering) => offering.classId === classRoom.id),
  );
  const courseOptions = offeringOptions.filter((offering) => offering.classId === classId);

  const loadingOptions = offerings.isLoading || classes.isLoading;
  const errors = form.formState.errors;

  return (
    <FormDrawer
      open={open}
      onOpenChange={onOpenChange}
      title="Assign a teacher"
      description="Links one teacher to one course offering. This is what lets that teacher create assignments and grade submissions for that class and course."
      submitLabel="Assign"
      submitting={create.isPending}
      onSubmit={form.handleSubmit(onSubmit)}
      width="sm"
    >
      <div className="space-y-2">
        <Label htmlFor="teacherId">Teacher</Label>
        <Combobox
          id="teacherId"
          value={form.watch('teacherId')}
          onChange={(value) => form.setValue('teacherId', value, { shouldValidate: true })}
          options={teacherOptions.map((teacher) => ({
            value: teacher.id,
            label: teacher.fullName,
            hint: teacher.email,
          }))}
          placeholder={teachers.isLoading ? 'Loading…' : 'Choose a teacher'}
          searchPlaceholder="Search name or email…"
          emptyMessage="No teachers match"
          aria-invalid={!!errors.teacherId}
          clearable
        />
        {errors.teacherId && <p className="text-xs text-danger">{errors.teacherId.message}</p>}
      </div>

      <ClassPicker
        classes={classOptions}
        loading={loadingOptions}
        value={classId}
        onChange={(value) => {
          setPendingClassId(value);
          // The chosen course belongs to the old cohort, so it cannot survive the change.
          form.setValue('classCourseId', '');
        }}
        // Only these two boxes are at fault when nothing is chosen yet; once a class is
        // picked the missing half is the course, and that is where the message belongs.
        invalid={!!errors.classCourseId && !classId}
        idPrefix="mapping-class"
      />

      <div className="space-y-2">
        <Label htmlFor="classCourseId">Course</Label>
        <Combobox
          id="classCourseId"
          value={classCourseId}
          onChange={(value) => form.setValue('classCourseId', value, { shouldValidate: true })}
          // The code is a hint rather than part of the label so that searching for it works
          // without the option list reading as a wall of parenthesised codes.
          options={courseOptions.map((offering) => ({
            value: offering.id,
            label: offering.courseName,
            hint: offering.courseCode,
          }))}
          placeholder={classId ? 'Choose a course' : 'Choose a class and section first'}
          searchPlaceholder="Search name or code…"
          emptyMessage="This cohort has no course left to assign"
          disabled={loadingOptions || !classId}
          aria-invalid={!!errors.classCourseId}
          clearable
        />
        {errors.classCourseId && (
          <p className="text-xs text-danger">{errors.classCourseId.message}</p>
        )}
        {!loadingOptions && offeringOptions.length === 0 && (
          <p className="text-xs text-muted-foreground">
            {(offerings.data ?? []).length === 0
              ? 'No offerings yet — add a course to a class on the Offerings screen first.'
              : 'Every offering already has a teacher. Remove a mapping on this screen to free one up.'}
          </p>
        )}
      </div>
    </FormDrawer>
  );
}
