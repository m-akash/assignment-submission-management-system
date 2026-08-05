import {
  Backpack,
  BookOpen,
  ClipboardList,
  GraduationCap,
  Inbox,
  Layers,
  LayoutDashboard,
  Link2,
  Mail,
  UserCog,
  Users,
  type LucideIcon,
} from 'lucide-react';
import type { Role } from '@/types/api';

export interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  roles: Role[];
  /** Groups the sidebar into sections. */
  section: 'Overview' | 'Coursework' | 'Administration';
  /**
   * Set when this link is the users page under a preset role filter. Teachers and
   * students are one screen, not three: all three roles live in a single table behind
   * one set of endpoints, so the role travels in the URL instead of being duplicated
   * into separate pages, forms and hooks.
   */
  roleParam?: Role;
}

/**
 * The navigation is derived from role, so each person sees only the pages that exist
 * for them. This mirrors the API's authorization rather than replacing it — hiding a
 * link is a courtesy; the server still refuses the request.
 */
export const NAV_ITEMS: NavItem[] = [
  {
    href: '/',
    label: 'Dashboard',
    icon: LayoutDashboard,
    roles: ['Admin', 'Teacher', 'Student'],
    section: 'Overview',
  },
  {
    href: '/assignments',
    label: 'Assignments',
    icon: ClipboardList,
    roles: ['Admin', 'Teacher', 'Student'],
    section: 'Coursework',
  },
  {
    href: '/submissions',
    label: 'Submissions',
    icon: Inbox,
    roles: ['Admin', 'Teacher'],
    section: 'Coursework',
  },
  {
    href: '/my-courses',
    label: 'My courses',
    icon: GraduationCap,
    roles: ['Teacher'],
    section: 'Coursework',
  },
  {
    // Students reach their courses through their class, not a teaching assignment, so this
    // is a separate route rather than a role branch on /my-courses.
    href: '/enrolled-courses',
    label: 'My courses',
    icon: GraduationCap,
    roles: ['Student'],
    section: 'Coursework',
  },
  {
    href: '/my-students',
    label: 'My students',
    icon: Backpack,
    roles: ['Teacher'],
    section: 'Coursework',
  },
  { href: '/users', label: 'All Users', icon: Users, roles: ['Admin'], section: 'Administration' },
  {
    href: '/users?role=Teacher',
    label: 'Teachers',
    icon: UserCog,
    roleParam: 'Teacher',
    roles: ['Admin'],
    section: 'Administration',
  },
  {
    href: '/users?role=Student',
    label: 'Students',
    icon: Backpack,
    roleParam: 'Student',
    roles: ['Admin'],
    section: 'Administration',
  },
  {
    href: '/classes',
    label: 'Classes',
    icon: GraduationCap,
    roles: ['Admin'],
    section: 'Administration',
  },
  { href: '/courses', label: 'Courses', icon: BookOpen, roles: ['Admin'], section: 'Administration' },
  {
    href: '/class-courses',
    label: 'Course Offerings',
    icon: Layers,
    roles: ['Admin'],
    section: 'Administration',
  },
  {
    href: '/teacher-mappings',
    label: 'Teaching Assignments',
    icon: Link2,
    roles: ['Admin'],
    section: 'Administration',
  },
  {
    href: '/notifications',
    label: 'Notifications',
    icon: Mail,
    roles: ['Admin'],
    section: 'Administration',
  },
];

export const NAV_SECTIONS = ['Overview', 'Coursework', 'Administration'] as const;

export function navItemsFor(role: Role): NavItem[] {
  return NAV_ITEMS.filter((item) => item.roles.includes(role));
}

/**
 * The nav entry the visitor is currently on, which the header renders as a breadcrumb.
 * Longest base path first so `/users?role=Student` wins over the bare `/users` entry.
 */
export function currentNavItem(
  role: Role,
  pathname: string,
  roleParam: string,
): NavItem | undefined {
  return navItemsFor(role)
    .filter((item) => isNavItemActive(item, pathname, roleParam))
    .sort((a, b) => b.href.length - a.href.length)[0];
}

/** Base paths where several links share a pathname and differ only by `?role=`. */
const ROLE_FILTERED_BASES = new Set(
  NAV_ITEMS.filter((item) => item.roleParam).map((item) => basePathOf(item.href)),
);

function basePathOf(href: string): string {
  return href.split('?')[0];
}

/**
 * Whether a nav link represents the current location. Prefix matching alone is not
 * enough: "Teachers", "Students" and "All users" all point at `/users`, so on those
 * the active link is decided by the role in the query string.
 */
export function isNavItemActive(item: NavItem, pathname: string, roleParam: string): boolean {
  // "/" would otherwise match every route.
  if (item.href === '/') return pathname === '/';

  const base = basePathOf(item.href);
  if (!pathname.startsWith(base)) return false;

  return ROLE_FILTERED_BASES.has(base) ? (item.roleParam ?? '') === roleParam : true;
}
