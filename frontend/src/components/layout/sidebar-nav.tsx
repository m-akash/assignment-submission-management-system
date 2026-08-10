'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import { ChevronsUpDown, GraduationCap, LogOut } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  useSidebar,
} from '@/components/ui/sidebar';
import { RoleBadge } from '@/components/shared/status-badge';
import { useAuth } from '@/context/AuthContext';
import { initials } from '@/lib/format';
import { NAV_SECTIONS, isNavItemActive, navItemsFor } from './nav-items';
import type { AuthUser } from '@/types/api';

/**
 * The application's navigation, on shadcn's `Sidebar`. It is the same table of links as
 * before, but the behaviour around them is now the component's rather than this file's:
 * a persistent rail on desktop that collapses to icons (and remembers that choice in a
 * cookie), a sheet on small screens, and ⌘/Ctrl-B to toggle either.
 */
export function SidebarNav({ user }: { user: AuthUser }) {
  const pathname = usePathname();
  // The users page is reachable under a preset ?role=, so the query string is part of
  // deciding which link is active.
  const currentRoleParam = useSearchParams().get('role') ?? '';
  const items = navItemsFor(user.role);
  // Following a link inside the mobile sheet should leave it behind, not on top of the
  // page it just opened. On desktop the sidebar stays where it is.
  const { isMobile, setOpenMobile } = useSidebar();

  function onNavigate() {
    if (isMobile) setOpenMobile(false);
  }

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader className="border-b border-sidebar-border">
        <SidebarMenu>
          <SidebarMenuItem>
            {/* Brand lockup. Its height matches the header's so the two align across
                the seam. */}
            <SidebarMenuButton asChild size="lg" tooltip="Assignment Management System">
              <Link href="/" onClick={onNavigate}>
                <span className="brand-surface flex aspect-square size-8 shrink-0 items-center justify-center rounded-lg text-white shadow-sm">
                  <GraduationCap className="size-5" />
                </span>
                <div className="grid min-w-0 flex-1 leading-tight">
                  <span className="truncate font-heading text-[0.95rem] font-semibold tracking-tight">
                    Assignment
                  </span>
                  <span className="truncate text-[0.7rem] tracking-wide text-muted-foreground">
                    Management System
                  </span>
                </div>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent className="gap-0 py-2">
        {NAV_SECTIONS.map((section) => {
          const sectionItems = items.filter((item) => item.section === section);
          if (sectionItems.length === 0) return null;

          return (
            <SidebarGroup key={section}>
              <SidebarGroupLabel className="eyebrow">{section}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  {sectionItems.map((item) => {
                    const { href, label, icon: Icon } = item;
                    const active = isNavItemActive(item, pathname, currentRoleParam);

                    return (
                      <SidebarMenuItem key={href}>
                        <SidebarMenuButton
                          asChild
                          isActive={active}
                          tooltip={label}
                          className="data-active:bg-primary/10 data-active:font-semibold data-active:text-primary"
                        >
                          <Link
                            href={href}
                            onClick={onNavigate}
                            aria-current={active ? 'page' : undefined}
                          >
                            <Icon />
                            <span>{label}</span>
                          </Link>
                        </SidebarMenuButton>
                      </SidebarMenuItem>
                    );
                  })}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          );
        })}
      </SidebarContent>

      {/* Signed-in account. On desktop this is the only place it lives — the header keeps
          a menu for small screens, where this footer is behind the navigation sheet.
          A menu rather than a button beside the name, because the rail collapses to icons
          and signing out has to survive that. */}
      <SidebarFooter className="border-t border-sidebar-border">
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton size="lg" tooltip={user.fullName}>
                  <Avatar className="size-8 shrink-0">
                    <AvatarFallback className="bg-primary/10 text-[11px] font-semibold text-primary">
                      {initials(user.fullName)}
                    </AvatarFallback>
                  </Avatar>
                  <div className="grid min-w-0 flex-1 justify-items-start gap-1 leading-tight">
                    <span className="max-w-full truncate text-sm font-medium">
                      {user.fullName}
                    </span>
                    <RoleBadge role={user.role} />
                  </div>
                  <ChevronsUpDown className="ml-auto opacity-60" />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                side={isMobile ? 'bottom' : 'right'}
                align="end"
                sideOffset={8}
                className="w-60"
              >
                <DropdownMenuLabel className="space-y-1.5">
                  <p className="truncate font-medium">{user.fullName}</p>
                  <p className="truncate text-xs font-normal text-muted-foreground">
                    {user.email}
                  </p>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <SignOutItem />
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>

      {/* The draggable seam: the whole edge toggles the sidebar, so collapsing it does
          not mean finding a small button first. */}
      <SidebarRail />
    </Sidebar>
  );
}

function SignOutItem() {
  const { logout } = useAuth();

  return (
    <DropdownMenuItem onClick={() => void logout()}>
      <LogOut className="size-4" />
      Sign out
    </DropdownMenuItem>
  );
}
