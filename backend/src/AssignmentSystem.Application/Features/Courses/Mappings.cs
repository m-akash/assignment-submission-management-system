using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Courses;

namespace AssignmentSystem.Application.Features.Courses;

[Mapper]
public partial class CourseMapper
{
    [MapProperty("Department.Name", nameof(CourseDto.DepartmentName))]
    [MapProperty("Department.Code", nameof(CourseDto.DepartmentCode))]
    [MapProperty("Group.Name", nameof(CourseDto.GroupName))]
    public partial CourseDto MapToDto(Course course);
}
