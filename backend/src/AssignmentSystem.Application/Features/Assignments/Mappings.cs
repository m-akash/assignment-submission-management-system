using Riok.Mapperly.Abstractions;
using AssignmentSystem.Application.Features.AssignmentFiles;
using AssignmentSystem.Domain.Assignments;

namespace AssignmentSystem.Application.Features.Assignments;

[Mapper]
public partial class AssignmentMapper
{
    [MapProperty("TeacherAssignment.Teacher.FullName", nameof(AssignmentDto.TeacherName))]
    [MapProperty("Course.Name", nameof(AssignmentDto.CourseName))]
    [MapProperty("Course.Code", nameof(AssignmentDto.CourseCode))]
    [MapProperty("Class.Name", nameof(AssignmentDto.ClassName))]
    public partial AssignmentDto MapToDto(Assignment assignment);

    public partial AssignmentFileDto MapToFileDto(AssignmentFile file);
}
