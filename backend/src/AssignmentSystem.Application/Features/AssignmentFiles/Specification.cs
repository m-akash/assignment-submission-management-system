using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;

namespace AssignmentSystem.Application.Features.AssignmentFiles;

internal sealed class AssignmentFileByIdSpecification : Specification<AssignmentFile>
{
    public AssignmentFileByIdSpecification(Guid fileId)
    {
        Criteria = f => f.Id == fileId;
        AddInclude(f => f.Assignment);
        AddInclude("Assignment.Course");
    }
}

internal sealed class AssignmentFilesByAssignmentSpecification : Specification<AssignmentFile>
{
    public AssignmentFilesByAssignmentSpecification(Guid assignmentId)
    {
        Criteria = f => f.AssignmentId == assignmentId;
    }
}
