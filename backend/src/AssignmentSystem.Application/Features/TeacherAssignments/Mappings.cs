using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

[Mapper]
public partial class TeacherAssignmentMapper
{
    [MapProperty("Teacher.FullName", nameof(TeacherAssignmentDto.TeacherName))]
    [MapProperty("Teacher.Email.Value", nameof(TeacherAssignmentDto.TeacherEmail))]
    [MapProperty("Subject.Name", nameof(TeacherAssignmentDto.SubjectName))]
    [MapProperty("Subject.Code", nameof(TeacherAssignmentDto.SubjectCode))]
    [MapProperty("Class.Name", nameof(TeacherAssignmentDto.ClassName))]
    public partial TeacherAssignmentDto MapToDto(TeacherAssignment teacherAssignment);
}
