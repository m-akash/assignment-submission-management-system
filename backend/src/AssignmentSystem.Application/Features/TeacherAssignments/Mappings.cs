using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

[Mapper]
public partial class TeacherAssignmentMapper
{
    [MapProperty("Teacher.FullName", nameof(TeacherAssignmentDto.TeacherName))]
    [MapProperty("Teacher.Email.Value", nameof(TeacherAssignmentDto.TeacherEmail))]
    [MapProperty("Course.Name", nameof(TeacherAssignmentDto.CourseName))]
    [MapProperty("Course.Code", nameof(TeacherAssignmentDto.CourseCode))]
    [MapProperty("Class.Name", nameof(TeacherAssignmentDto.ClassName))]
    public partial TeacherAssignmentDto MapToDto(TeacherAssignment teacherAssignment);
}
