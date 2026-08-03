'use client';

import { useAuth } from '@/context/AuthContext';
import { AdminOverview } from '@/components/features/overview/admin-overview';
import { StudentOverview } from '@/components/features/overview/student-overview';
import { TeacherOverview } from '@/components/features/overview/teacher-overview';

export default function DashboardPage() {
  const { user } = useAuth();
  if (!user) return null;

  if (user.role === 'Admin') return <AdminOverview />;
  if (user.role === 'Teacher') return <TeacherOverview name={user.fullName} />;
  return <StudentOverview name={user.fullName} className={user.className} />;
}
