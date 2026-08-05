'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import { GraduationCap, LogOut } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { RoleBadge } from '@/components/shared/status-badge';
import { useAuth } from '@/context/AuthContext';
import { initials } from '@/lib/format';
import { cn } from '@/lib/utils';
import { NAV_SECTIONS, isNavItemActive, navItemsFor } from './nav-items';
import type { AuthUser } from '@/types/api';

export function SidebarNav({ user, onNavigate }: { user: AuthUser; onNavigate?: () => void }) {
  const pathname = usePathname();
  // The users page is reachable under a preset ?role=, so the query string is part of
  // deciding which link is active.
  const currentRoleParam = useSearchParams().get('role') ?? '';
  const items = navItemsFor(user.role);

  return (
    <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
      {/* Brand lockup. Its height matches the header's so the two align across the seam. */}
      <Link
        href="/"
        onClick={onNavigate}
        className="flex h-16 shrink-0 items-center gap-3 border-b border-sidebar-border px-4"
      >
        <span className="brand-surface flex size-9 shrink-0 items-center justify-center rounded-xl text-white shadow-sm">
          <GraduationCap className="size-5" />
        </span>
        <span className="min-w-0 leading-tight">
          <span className="block truncate font-heading text-[0.95rem] font-semibold tracking-tight">
            Assignment
          </span>
          <span className="block truncate text-[0.7rem] tracking-wide text-muted-foreground">
            Management System
          </span>
        </span>
      </Link>

      <nav className="scroll-slim flex-1 space-y-6 overflow-y-auto px-3 py-5">
        {NAV_SECTIONS.map((section) => {
          const sectionItems = items.filter((item) => item.section === section);
          if (sectionItems.length === 0) return null;

          return (
            <div key={section} className="space-y-1">
              <p className="eyebrow px-2.5 pb-1">{section}</p>
              {sectionItems.map((item) => {
                const { href, label, icon: Icon } = item;
                const active = isNavItemActive(item, pathname, currentRoleParam);

                return (
                  <Link
                    key={href}
                    href={href}
                    onClick={onNavigate}
                    aria-current={active ? 'page' : undefined}
                    className={cn(
                      'relative flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm transition-colors',
                      active
                        ? 'bg-primary/10 font-semibold text-primary'
                        : 'font-medium text-muted-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                    )}
                  >
                    {/* A rail flush with the sidebar edge, so the active page is findable
                        from the page's own left margin rather than by reading labels. */}
                    {active && (
                      <span
                        aria-hidden
                        className="absolute top-1/2 -left-3 h-5 w-0.75 -translate-y-1/2 rounded-r-full bg-primary"
                      />
                    )}
                    <Icon className="size-4 shrink-0" />
                    <span className="truncate">{label}</span>
                  </Link>
                );
              })}
            </div>
          );
        })}
      </nav>

      {/* Signed-in account. On desktop this is the only place it lives — the header keeps
          a menu for small screens, where this footer is behind the navigation sheet. */}
      <div className="shrink-0 border-t border-sidebar-border p-3">
        <div className="flex items-center gap-2.5 rounded-lg px-1.5 py-1.5">
          <Avatar className="size-8 shrink-0">
            <AvatarFallback className="bg-primary/10 text-[11px] font-semibold text-primary">
              {initials(user.fullName)}
            </AvatarFallback>
          </Avatar>
          <div className="min-w-0 flex-1 space-y-1">
            <p className="truncate text-sm font-medium">{user.fullName}</p>
            <RoleBadge role={user.role} />
          </div>
          <SignOutButton />
        </div>
      </div>
    </div>
  );
}

function SignOutButton() {
  const { logout } = useAuth();

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          variant="ghost"
          size="icon-sm"
          onClick={() => void logout()}
          aria-label="Sign out"
          className="shrink-0 text-muted-foreground hover:text-foreground"
        >
          <LogOut className="size-4" />
        </Button>
      </TooltipTrigger>
      <TooltipContent side="top">Sign out</TooltipContent>
    </Tooltip>
  );
}
