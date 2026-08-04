using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;

namespace AssignmentSystem.Application.Features.AssignmentFiles;

internal sealed class AssignmentFileByIdSpecification : Specification<AssignmentFile>
{
    public AssignmentFileByIdSpecification(Guid fileId)
    {
        Criteria = f => f.Id == fileId;
        AddInclude(f => f.Assignment);
        // Two levels: the student download check reads the offering's class id to test
        // enrollment (rule B1), so the navigation has to be loaded, not just the assignment.
        AddInclude("Assignment.ClassCourse");
    }
}

internal sealed class AssignmentFilesByAssignmentSpecification : Specification<AssignmentFile>
{
    public AssignmentFilesByAssignmentSpecification(Guid assignmentId)
    {
        Criteria = f => f.AssignmentId == assignmentId;
    }
}
