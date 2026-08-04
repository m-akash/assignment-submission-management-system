using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Courses;

namespace AssignmentSystem.Application.Features.Courses;

[Mapper]
public partial class CourseMapper
{
    public partial CourseDto MapToDto(Course course);
}
