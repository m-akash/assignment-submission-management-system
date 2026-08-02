using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

internal sealed class TeacherAssignmentWithDetailsSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = ta => ta.Id == id;
        AddInclude(ta => ta.Teacher);
        AddInclude(ta => ta.Subject);
        AddInclude(ta => ta.Class);
    }
}

internal sealed class TeacherAssignmentDuplicateSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentDuplicateSpecification(Guid teacherId, Guid subjectId, Guid classId)
    {
        Criteria = ta => ta.TeacherId == teacherId && ta.SubjectId == subjectId && ta.ClassId == classId;
    }
}

internal sealed class TeacherAssignmentsPagedSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentsPagedSpecification(Guid? teacherId, Guid? subjectId, Guid? classId, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(ta => ta.Teacher);
        AddInclude(ta => ta.Subject);
        AddInclude(ta => ta.Class);
        ApplyOrderBy(ta => ta.Teacher.FullName);
        ApplyPaging(page, pageSize);

        Criteria = ta =>
            (!teacherId.HasValue || ta.TeacherId == teacherId.Value) &&
            (!subjectId.HasValue || ta.SubjectId == subjectId.Value) &&
            (!classId.HasValue || ta.ClassId == classId.Value);
    }
}
