'use client';

import { useParams } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { StudentAssignmentDetail } from '@/components/features/assignments/student-assignment-detail';
import { TeacherAssignmentDetail } from '@/components/features/assignments/teacher-assignment-detail';

/**
 * One assignment, two very different questions: a student asks what to do and hands it
 * in, a teacher asks how the class is getting on with it. An admin sees the teacher's
 * view without the controls.
 */
export default function AssignmentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();

  if (!user) return null;

  return user.role === 'Student' ? (
    <StudentAssignmentDetail assignmentId={id} />
  ) : (
    <TeacherAssignmentDetail assignmentId={id} readOnly={user.role === 'Admin'} />
  );
}
