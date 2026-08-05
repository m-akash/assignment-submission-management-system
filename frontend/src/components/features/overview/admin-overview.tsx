'use client';

import Link from 'next/link';
import {
  ArrowRight,
  Backpack,
  BookOpen,
  ClipboardList,
  GraduationCap,
  Inbox,
  Layers,
  Link2,
  UserCog,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { HeroBanner, HeroStat, heroButton } from '@/components/shared/hero-banner';
import { SectionPanel } from '@/components/shared/section-panel';
import { StatCard } from '@/components/shared/stat-card';
import { useAssignments } from '@/hooks/use-assignments';
import {
  useClassCourses,
  useClasses,
  useCourses,
  useTeacherMappings,
  useUsers,
} from '@/hooks/use-admin-resources';
import { useSubmissions } from '@/hooks/use-submissions';

/**
 * Counts come from each list endpoint's pagination total with `pageSize=1`, so the
 * server does the counting and only one row travels per tile.
 */
const COUNT_ONLY = { page: 1, pageSize: 1 };

/** The order a school has to be built in — a teacher cannot set work before step 4. */
const SETUP_STEPS = [
  {
    href: '/classes',
    label: 'Classes',
    step: 'Create the class',
    detail: 'Level and section, e.g. Class 9 — A.',
    icon: GraduationCap,
  },
  {
    href: '/courses',
    label: 'Courses',
    step: 'Add the course',
    detail: 'A subject with a unique code.',
    icon: BookOpen,
  },
  {
    href: '/class-courses',
    label: 'Offerings',
    step: 'Offer it to the class',
    detail: 'Pair a course with a class.',
    icon: Layers,
  },
  {
    href: '/teacher-mappings',
    label: 'Teaching',
    step: 'Assign a teacher',
    detail: 'Only then can work be set.',
    icon: Link2,
  },
] as const;

export function AdminOverview({ name }: { name: string }) {
  const students = useUsers({ ...COUNT_ONLY, role: 'Student' });
  const teachers = useUsers({ ...COUNT_ONLY, role: 'Teacher' });
  const classes = useClasses(COUNT_ONLY);
  const courses = useCourses(COUNT_ONLY);
  const offerings = useClassCourses(COUNT_ONLY);
  const mappings = useTeacherMappings(COUNT_ONLY);
  const assignments = useAssignments(COUNT_ONLY);
  const submissions = useSubmissions(COUNT_ONLY);

  const firstName = name.split(' ')[0];
  const studentCount = students.data?.pagination.total ?? 0;
  const teacherCount = teachers.data?.pagination.total ?? 0;

  return (
    <div className="space-y-6">
      <HeroBanner
        eyebrow="Administration"
        title={`Welcome back, ${firstName}`}
        description="Everything in the school at a glance — people, coursework, and the links between them. Use the panels below to manage each."
        actions={
          <>
            <Button asChild size="lg" className={heroButton.solid}>
              <Link href="/users">
                <Users className="size-4" />
                Manage users
              </Link>
            </Button>
            <Button asChild size="lg" variant="outline" className={heroButton.quiet}>
              <Link href="/teacher-mappings">Teaching assignments</Link>
            </Button>
          </>
        }
        aside={
          <div className="flex gap-3">
            <HeroStat value={studentCount} label="Students" />
            <HeroStat value={teacherCount} label="Teachers" />
            <HeroStat value={classes.data?.pagination.total ?? 0} label="Classes" />
          </div>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Students"
          value={studentCount}
          hint="Enrolled in at least one class"
          icon={Backpack}
          tone="success"
          loading={students.isLoading}
          href="/users?role=Student"
        />
        <StatCard
          label="Teachers"
          value={teacherCount}
          hint="Available to teach an offering"
          icon={UserCog}
          tone="info"
          loading={teachers.isLoading}
          href="/users?role=Teacher"
        />
        <StatCard
          label="Classes"
          value={classes.data?.pagination.total ?? 0}
          hint="Level and section"
          icon={GraduationCap}
          tone="primary"
          loading={classes.isLoading}
          href="/classes"
        />
        <StatCard
          label="Courses"
          value={courses.data?.pagination.total ?? 0}
          hint="Subjects on offer"
          icon={BookOpen}
          tone="warning"
          loading={courses.isLoading}
          href="/courses"
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Course offerings"
          value={offerings.data?.pagination.total ?? 0}
          hint="Course · class pairs"
          icon={Layers}
          loading={offerings.isLoading}
          href="/class-courses"
        />
        <StatCard
          label="Teaching assignments"
          value={mappings.data?.pagination.total ?? 0}
          hint="Teacher · offering links"
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

      <SectionPanel
        title="Set up a class"
        description="A teacher can only create assignments for an offering they are assigned to, so the order matters."
        icon={Layers}
        bodyClassName="p-5"
      >
        <ol className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {SETUP_STEPS.map(({ href, label, step, detail, icon: Icon }, index) => (
            <li key={href} className="relative">
              {/* A connector between steps, drawn only where there is a next step to
                  point at — the sequence is the whole point of this panel. */}
              {index < SETUP_STEPS.length - 1 && (
                <ArrowRight
                  aria-hidden
                  className="absolute top-1/2 -right-2.5 z-10 hidden size-4 -translate-y-1/2 text-muted-foreground/60 xl:block"
                />
              )}
              <Link
                href={href}
                className="panel-interactive flex h-full flex-col gap-2 bg-muted/25 p-4"
              >
                <div className="flex items-center justify-between">
                  <span className="flex size-7 items-center justify-center rounded-lg bg-primary/10 font-heading text-xs font-semibold text-primary">
                    {index + 1}
                  </span>
                  <Icon aria-hidden className="size-4 text-muted-foreground" />
                </div>
                <p className="text-sm font-medium">{step}</p>
                <p className="text-xs text-muted-foreground">{detail}</p>
                <span className="mt-auto pt-2 text-xs font-medium text-primary">{label} →</span>
              </Link>
            </li>
          ))}
        </ol>
      </SectionPanel>
    </div>
  );
}
