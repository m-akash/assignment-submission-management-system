'use client';

import { useSyncExternalStore } from 'react';
import { usePathname, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useTheme } from 'next-themes';
import { ChevronRight, GraduationCap, LogOut, MoonStar, Sun } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { SidebarInset, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { RoleBadge } from '@/components/shared/status-badge';
import { useAuth } from '@/context/AuthContext';
import { classLabel, initials } from '@/lib/format';
import { currentNavItem } from './nav-items';
import { SidebarNav } from './sidebar-nav';
import type { AuthUser } from '@/types/api';

/**
 * Whether the sidebar was left open, from the cookie `SidebarProvider` writes. Read here
 * rather than on the server because the shell is only ever reached after the session has
 * been restored on the client — there is no server render of it to disagree with.
 */
function sidebarDefaultOpen(): boolean {
  if (typeof document === 'undefined') return true;
  return !/(?:^|;\s*)sidebar_state=false(?:;|$)/.test(document.cookie);
}

export function AppShell({ user, children }: { user: AuthUser; children: React.ReactNode }) {
  return (
    // The rail is wide enough for "Teaching Assignments" without wrapping, which the
    // 16rem default is not.
    <SidebarProvider
      defaultOpen={sidebarDefaultOpen()}
      style={{ '--sidebar-width': '16.5rem' } as React.CSSProperties}
    >
      <SidebarNav user={user} />

      <SidebarInset>
        <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b bg-background/80 px-4 backdrop-blur-md sm:px-6 lg:px-8">
          <SidebarTrigger className="-ml-1 shrink-0 text-muted-foreground hover:text-foreground" />

          <Breadcrumb user={user} />

          <div className="ml-auto flex items-center gap-1.5">
            {user.classes.length > 0 && (
              // A student's enrolment is stated once, here: several classes are possible,
              // so they are all listed rather than only the first.
              <div className="hidden items-center gap-1.5 sm:flex">
                {user.classes.map((enrolled) => (
                  <span
                    key={enrolled.classId}
                    className="rounded-full border bg-card px-2.5 py-1 text-xs font-medium"
                  >
                    {classLabel(enrolled.classLevel, enrolled.classSection)}
                  </span>
                ))}
              </div>
            )}
            <ThemeToggle />
            {/* From md up the account lives in the sidebar footer; this is its
                small-screen home, where that footer is behind the navigation sheet. */}
            <div className="md:hidden">
              <UserMenu user={user} />
            </div>
          </div>
        </header>

        <div className="flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <div className="animate-rise mx-auto w-full max-w-350">{children}</div>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}

/**
 * Where the visitor is, derived from the same nav table the sidebar renders from — so a
 * new page cannot appear in one and be missing from the other.
 */
function Breadcrumb({ user }: { user: AuthUser }) {
  const pathname = usePathname();
  const roleParam = useSearchParams().get('role') ?? '';
  const current = currentNavItem(user.role, pathname, roleParam);
  const onDashboard = pathname === '/';

  return (
    <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-1.5 text-sm">
      <Link
        href="/"
        className="flex shrink-0 items-center gap-2 font-medium text-muted-foreground transition-colors hover:text-foreground md:hidden"
      >
        <GraduationCap className="size-4" />
      </Link>
      {onDashboard ? (
        <span className="truncate font-medium">Dashboard</span>
      ) : (
        <>
          <Link
            href="/"
            className="hidden shrink-0 text-muted-foreground transition-colors hover:text-foreground sm:inline"
          >
            Dashboard
          </Link>
          <ChevronRight aria-hidden className="hidden size-3.5 shrink-0 text-muted-foreground sm:inline" />
          <span className="truncate font-medium">{current?.label ?? 'Page'}</span>
        </>
      )}
    </nav>
  );
}

function UserMenu({ user }: { user: AuthUser }) {
  const { logout } = useAuth();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon-lg" aria-label="Account">
          <Avatar className="size-7">
            <AvatarFallback className="bg-primary/10 text-[11px] font-semibold text-primary">
              {initials(user.fullName)}
            </AvatarFallback>
          </Avatar>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuLabel className="space-y-1.5">
          <p className="truncate font-medium">{user.fullName}</p>
          <p className="truncate text-xs font-normal text-muted-foreground">{user.email}</p>
          <RoleBadge role={user.role} />
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => void logout()}>
          <LogOut className="size-4" />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/**
 * `useSyncExternalStore` returns the server snapshot (false) during SSR and the
 * client snapshot (true) after hydration — a mount flag without a setState-in-effect.
 */
function useMounted() {
  return useSyncExternalStore(
    () => () => {},
    () => true,
    () => false,
  );
}

/**
 * Toggles between light and dark. Persisted via `next-themes` so the choice
 * survives refresh; the resolved theme is only read after mount to avoid a
 * hydration mismatch (server can't know the stored/`<html>` class yet).
 */
function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const mounted = useMounted();

  const dark = mounted && resolvedTheme === 'dark';

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          variant="ghost"
          size="icon-lg"
          onClick={() => setTheme(dark ? 'light' : 'dark')}
          aria-label="Toggle colour theme"
          className="text-muted-foreground hover:text-foreground"
        >
          {/* Render the same icon pre-mount on both server and client to keep markup stable. */}
          {dark ? <Sun className="size-4.5" /> : <MoonStar className="size-4.5" />}
        </Button>
      </TooltipTrigger>
      <TooltipContent side="bottom">{dark ? 'Light theme' : 'Dark theme'}</TooltipContent>
    </Tooltip>
  );
}
