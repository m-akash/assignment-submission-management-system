'use client';

import { useAuth } from '@/context/AuthContext';
import { StudentAssignmentsView } from '@/components/features/assignments/student-assignments-view';
import { TeacherAssignmentsView } from '@/components/features/assignments/teacher-assignments-view';

/**
 * One route, two very different jobs: a teacher manages the assignments they own, a
 * student works through the ones set for their class.
 */
export default function AssignmentsPage() {
  const { user } = useAuth();
  if (!user) return null;

  return user.role === 'Student' ? <StudentAssignmentsView /> : <TeacherAssignmentsView />;
}
