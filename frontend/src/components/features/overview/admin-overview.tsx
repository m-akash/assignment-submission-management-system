'use client';

import Link from 'next/link';
import { Backpack, BookOpen, ClipboardList, GraduationCap, Inbox, Link2, UserCog } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/shared/page-header';
import { StatCard } from '@/components/shared/stat-card';
import { useAssignments } from '@/hooks/use-assignments';
import { useClasses, useCourses, useTeacherMappings, useUsers } from '@/hooks/use-admin-resources';
import { useSubmissions } from '@/hooks/use-submissions';

/**
 * Counts come from each list endpoint's pagination total with `pageSize=1`, so the
 * server does the counting and only one row travels per tile.
 */
const COUNT_ONLY = { page: 1, pageSize: 1 };

export function AdminOverview() {
  const students = useUsers({ ...COUNT_ONLY, role: 'Student' });
  const teachers = useUsers({ ...COUNT_ONLY, role: 'Teacher' });
  const classes = useClasses(COUNT_ONLY);
  const courses = useCourses(COUNT_ONLY);
  const mappings = useTeacherMappings(COUNT_ONLY);
  const assignments = useAssignments(COUNT_ONLY);
  const submissions = useSubmissions(COUNT_ONLY);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Overview"
        description="Everything in the school at a glance. Use the sections below to manage people and coursework."
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Students"
          value={students.data?.pagination.total ?? 0}
          icon={Backpack}
          tone="success"
          loading={students.isLoading}
          href="/users?role=Student"
        />
        <StatCard
          label="Teachers"
          value={teachers.data?.pagination.total ?? 0}
          icon={UserCog}
          tone="info"
          loading={teachers.isLoading}
          href="/users?role=Teacher"
        />
        <StatCard
          label="Classes"
          value={classes.data?.pagination.total ?? 0}
          icon={GraduationCap}
          tone="primary"
          loading={classes.isLoading}
          href="/classes"
        />
        <StatCard
          label="Courses"
          value={courses.data?.pagination.total ?? 0}
          icon={BookOpen}
          tone="warning"
          loading={courses.isLoading}
          href="/courses"
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard
          label="Teaching assignments"
          value={mappings.data?.pagination.total ?? 0}
          hint="Teacher · course · class links"
          icon={Link2}
          loading={mappings.isLoading}
          href="/teacher-mappings"
        />
        <StatCard
          label="Assignments"
          value={assignments.data?.pagination.total ?? 0}
          hint="Drafts and published"
          icon={ClipboardList}
          loading={assignments.isLoading}
          href="/assignments"
        />
        <StatCard
          label="Submissions"
          value={submissions.data?.pagination.total ?? 0}
          hint="Across every class"
          icon={Inbox}
          loading={submissions.isLoading}
          href="/submissions"
        />
      </div>

      <div className="rounded-xl border bg-card p-5">
        <h2 className="font-medium">Set up a class</h2>
        <p className="mt-1 mb-4 text-sm text-muted-foreground">
          A teacher can only create assignments for a class and course they are assigned to, so the
          order matters.
        </p>
        <ol className="grid gap-3 sm:grid-cols-4">
          {[
            { step: 'Create the class', href: '/classes', label: 'Classes' },
            { step: 'Add the course', href: '/courses', label: 'Courses' },
            { step: 'Add teachers and students', href: '/users', label: 'All users' },
            { step: 'Assign a teacher to it', href: '/teacher-mappings', label: 'Teaching assignments' },
          ].map((item, index) => (
            <li key={item.href} className="rounded-lg border bg-background p-4">
              <span className="text-xs font-medium text-muted-foreground">Step {index + 1}</span>
              <p className="mt-1 mb-3 text-sm font-medium">{item.step}</p>
              <Button asChild variant="outline" size="sm">
                <Link href={item.href}>{item.label}</Link>
              </Button>
            </li>
          ))}
        </ol>
      </div>
    </div>
  );
}
