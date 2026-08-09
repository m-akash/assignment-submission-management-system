using Riok.Mapperly.Abstractions;
using AssignmentSystem.Application.Features.AssignmentFiles;
using AssignmentSystem.Domain.Assignments;

namespace AssignmentSystem.Application.Features.Assignments;

[Mapper]
public partial class AssignmentMapper
{
    [MapProperty("Teacher.FullName", nameof(AssignmentDto.TeacherName))]
    [MapProperty("ClassCourse.CourseId", nameof(AssignmentDto.CourseId))]
    [MapProperty("ClassCourse.Course.Name", nameof(AssignmentDto.CourseName))]
    [MapProperty("ClassCourse.Course.Code", nameof(AssignmentDto.CourseCode))]
    [MapProperty("ClassCourse.ClassId", nameof(AssignmentDto.ClassId))]
    [MapProperty("ClassCourse.Class.Level", nameof(AssignmentDto.ClassLevel))]
    [MapProperty("ClassCourse.Class.Section", nameof(AssignmentDto.ClassSection))]
    public partial AssignmentDto MapToDto(Assignment assignment);

    public partial AssignmentFileDto MapToFileDto(AssignmentFile file);
}
