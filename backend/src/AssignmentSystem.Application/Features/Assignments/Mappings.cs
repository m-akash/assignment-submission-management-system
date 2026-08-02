using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Assignments;

namespace AssignmentSystem.Application.Features.Assignments;

[Mapper]
public partial class AssignmentMapper
{
    [MapProperty("TeacherAssignment.Teacher.FullName", nameof(AssignmentDto.TeacherName))]
    [MapProperty("Subject.Name", nameof(AssignmentDto.SubjectName))]
    [MapProperty("Subject.Code", nameof(AssignmentDto.SubjectCode))]
    [MapProperty("Class.Name", nameof(AssignmentDto.ClassName))]
    public partial AssignmentDto MapToDto(Assignment assignment);
}
