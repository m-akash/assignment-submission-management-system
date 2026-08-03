import {
  BookOpen,
  ClipboardList,
  GraduationCap,
  Inbox,
  LayoutDashboard,
  Link2,
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
  { href: '/users', label: 'Users', icon: Users, roles: ['Admin'], section: 'Administration' },
  {
    href: '/classes',
    label: 'Classes',
    icon: GraduationCap,
    roles: ['Admin'],
    section: 'Administration',
  },
  { href: '/subjects', label: 'Subjects', icon: BookOpen, roles: ['Admin'], section: 'Administration' },
  {
    href: '/teacher-mappings',
    label: 'Teaching assignments',
    icon: Link2,
    roles: ['Admin'],
    section: 'Administration',
  },
];

export const NAV_SECTIONS = ['Overview', 'Coursework', 'Administration'] as const;

export function navItemsFor(role: Role): NavItem[] {
  return NAV_ITEMS.filter((item) => item.roles.includes(role));
}
