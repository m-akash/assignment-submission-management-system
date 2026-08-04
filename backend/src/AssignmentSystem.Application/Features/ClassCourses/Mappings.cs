using AssignmentSystem.Domain.ClassCourses;
using Riok.Mapperly.Abstractions;

namespace AssignmentSystem.Application.Features.ClassCourses;

[Mapper]
public partial class ClassCourseMapper
{
    // TeacherCount/AssignmentCount have no source property on ClassCourse — they are
    // aggregates the handler fills in afterwards, not something Mapperly can map.
    [MapProperty("Class.Name", nameof(ClassCourseDto.ClassName))]
    [MapProperty("Class.Level", nameof(ClassCourseDto.ClassLevel))]
    [MapProperty("Class.Section", nameof(ClassCourseDto.ClassSection))]
    [MapProperty("Course.Name", nameof(ClassCourseDto.CourseName))]
    [MapProperty("Course.Code", nameof(ClassCourseDto.CourseCode))]
    [MapperIgnoreTarget(nameof(ClassCourseDto.TeacherCount))]
    [MapperIgnoreTarget(nameof(ClassCourseDto.AssignmentCount))]
    public partial ClassCourseDto MapToDto(ClassCourse classCourse);
}
