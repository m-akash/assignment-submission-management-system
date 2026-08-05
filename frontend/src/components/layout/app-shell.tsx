'use client';

import { useSyncExternalStore, useState } from 'react';
import { useTheme } from 'next-themes';
import { LogOut, Menu, MoonStar, Sun } from 'lucide-react';
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
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { RoleBadge } from '@/components/shared/status-badge';
import { useAuth } from '@/context/AuthContext';
import { initials } from '@/lib/format';
import { SidebarNav } from './sidebar-nav';
import type { AuthUser } from '@/types/api';

export function AppShell({ user, children }: { user: AuthUser; children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <div className="flex min-h-dvh">
      {/* Persistent sidebar from lg up; a sheet below that. */}
      <aside className="hidden w-64 shrink-0 border-r bg-sidebar lg:block">
        <div className="sticky top-0 h-dvh">
          <SidebarNav role={user.role} />
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b bg-background/80 px-4 backdrop-blur-sm">
          <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open navigation">
                <Menu className="size-5" />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-64 p-0">
              <SheetTitle className="sr-only">Navigation</SheetTitle>
              <SidebarNav role={user.role} onNavigate={() => setMobileOpen(false)} />
            </SheetContent>
          </Sheet>

          <div className="min-w-0 flex-1">
            {user.classes.length > 0 && (
              <p className="truncate text-sm text-muted-foreground">
                {/* Joined rather than showing only the first: a student enrolled in two
                    classes needs to see both, and this is the only place it is stated. */}
                <span className="text-foreground">
                  {user.classes.map((enrolled) => enrolled.className).join(' · ')}
                </span>
              </p>
            )}
          </div>

          <ThemeToggle />
          <UserMenu user={user} />
        </header>

        <main className="mx-auto w-full max-w-[1400px] flex-1 p-4 sm:p-6 lg:p-8">{children}</main>
      </div>
    </div>
  );
}

function UserMenu({ user }: { user: AuthUser }) {
  const { logout } = useAuth();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-9 gap-2 px-2">
          <Avatar className="size-7">
            <AvatarFallback className="text-xs">{initials(user.fullName)}</AvatarFallback>
          </Avatar>
          <span className="hidden max-w-[140px] truncate text-sm font-medium sm:inline">
            {user.fullName}
          </span>
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

  const dark = resolvedTheme === 'dark';

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={() => setTheme(dark ? 'light' : 'dark')}
      aria-label="Toggle colour theme"
    >
      {/* Render the same icon pre-mount on both server and client to keep markup stable. */}
      {mounted && dark ? <Sun className="size-4" /> : <MoonStar className="size-4" />}
    </Button>
  );
}
