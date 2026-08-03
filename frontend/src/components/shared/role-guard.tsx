'use client';

import { ShieldAlert } from 'lucide-react';
import { EmptyState } from '@/components/shared/states';
import { useAuth } from '@/context/AuthContext';
import type { Role } from '@/types/api';

/**
 * Keeps a page's UI from rendering for a role it was not built for. The API enforces
 * the real rule — this only avoids showing controls that would be refused anyway.
 */
export function RoleGuard({ allow, children }: { allow: Role[]; children: React.ReactNode }) {
  const { user } = useAuth();

  if (!user) return null;

  if (!allow.includes(user.role)) {
    return (
      <EmptyState
        icon={ShieldAlert}
        title="Not available for your role"
        description="You do not have access to this section. Use the navigation to go somewhere you do."
      />
    );
  }

  return <>{children}</>;
}
