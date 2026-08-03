'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import { GraduationCap } from 'lucide-react';
import { cn } from '@/lib/utils';
import { NAV_SECTIONS, isNavItemActive, navItemsFor } from './nav-items';
import type { Role } from '@/types/api';

export function SidebarNav({ role, onNavigate }: { role: Role; onNavigate?: () => void }) {
  const pathname = usePathname();
  // The users page is reachable under a preset ?role=, so the query string is part of
  // deciding which link is active.
  const currentRoleParam = useSearchParams().get('role') ?? '';
  const items = navItemsFor(role);

  return (
    <div className="flex h-full flex-col gap-6 p-4">
      <Link href="/" className="flex items-center gap-2.5 px-2" onClick={onNavigate}>
        <div className="flex size-9 items-center justify-center rounded-lg bg-primary text-primary-foreground">
          <GraduationCap className="size-5" />
        </div>
        <div className="leading-tight">
          <p className="font-semibold tracking-tight">Scholaris</p>
          <p className="text-[11px] text-muted-foreground">Coursework portal</p>
        </div>
      </Link>

      <nav className="flex flex-1 flex-col gap-5">
        {NAV_SECTIONS.map((section) => {
          const sectionItems = items.filter((item) => item.section === section);
          if (sectionItems.length === 0) return null;

          return (
            <div key={section} className="space-y-1">
              <p className="px-2 text-[11px] font-medium tracking-wider text-muted-foreground uppercase">
                {section}
              </p>
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
                      'flex items-center gap-2.5 rounded-md px-2 py-2 text-sm font-medium transition-colors',
                      active
                        ? 'bg-primary/10 text-primary'
                        : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                    )}
                  >
                    <Icon className="size-4 shrink-0" />
                    <span className="truncate">{label}</span>
                  </Link>
                );
              })}
            </div>
          );
        })}
      </nav>
    </div>
  );
}
