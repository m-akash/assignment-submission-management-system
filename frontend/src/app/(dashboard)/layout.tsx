'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Loader2 } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { useAuth } from '@/context/AuthContext';

/**
 * Client-side gate for the authenticated shell. `proxy.ts` already turns away visitors
 * with no session cookie before the page is served; this handles the case where the
 * cookie exists but the session is no longer valid, and supplies the user object the
 * shell renders from.
 */
export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) {
      router.replace('/login');
    }
  }, [loading, user, router]);

  if (loading || !user) {
    return (
      <div className="flex min-h-dvh items-center justify-center gap-3 text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        <span className="text-sm">Restoring your session…</span>
      </div>
    );
  }

  return <AppShell user={user}>{children}</AppShell>;
}
