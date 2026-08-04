using AssignmentSystem.Application.Features.Enrollments;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Users;
using Riok.Mapperly.Abstractions;

namespace AssignmentSystem.Application.Features.Users;

[Mapper]
public partial class UserMapper
{
    [MapProperty(nameof(ApplicationUser.EmailValue), nameof(UserDto.Email))]
    [MapProperty(nameof(ApplicationUser.Enrollments), nameof(UserDto.Classes))]
    public partial UserDto MapToDto(ApplicationUser user);

    // Mapperly uses this to project each enrollment in the collection above. Declared here
    // rather than reusing EnrollmentMapper because a mapper can only reach methods on itself.
    [MapProperty(nameof(StudentEnrollment.Id), nameof(EnrolledClassDto.EnrollmentId))]
    [MapProperty("Class.Name", nameof(EnrolledClassDto.ClassName))]
    [MapProperty("Class.Level", nameof(EnrolledClassDto.ClassLevel))]
    [MapProperty("Class.Section", nameof(EnrolledClassDto.ClassSection))]
    private partial EnrolledClassDto MapEnrolledClass(StudentEnrollment enrollment);
}
