using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

[Mapper]
public partial class TeacherAssignmentMapper
{
    [MapProperty("Teacher.FullName", nameof(TeacherAssignmentDto.TeacherName))]
    [MapProperty("Teacher.Email.Value", nameof(TeacherAssignmentDto.TeacherEmail))]
    [MapProperty("ClassCourse.CourseId", nameof(TeacherAssignmentDto.CourseId))]
    [MapProperty("ClassCourse.Course.Name", nameof(TeacherAssignmentDto.CourseName))]
    [MapProperty("ClassCourse.Course.Code", nameof(TeacherAssignmentDto.CourseCode))]
    [MapProperty("ClassCourse.ClassId", nameof(TeacherAssignmentDto.ClassId))]
    [MapProperty("ClassCourse.Class.Level", nameof(TeacherAssignmentDto.ClassLevel))]
    [MapProperty("ClassCourse.Class.Section", nameof(TeacherAssignmentDto.ClassSection))]
    public partial TeacherAssignmentDto MapToDto(TeacherAssignment teacherAssignment);
}
