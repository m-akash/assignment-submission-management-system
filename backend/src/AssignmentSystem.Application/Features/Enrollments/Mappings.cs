using AssignmentSystem.Domain.Enrollments;
using Riok.Mapperly.Abstractions;

namespace AssignmentSystem.Application.Features.Enrollments;

[Mapper]
public partial class EnrollmentMapper
{
    [MapProperty("Student.FullName", nameof(EnrollmentDto.StudentName))]
    [MapProperty("Student.EmailValue", nameof(EnrollmentDto.StudentEmail))]
    // "StudentNumber", not "StudentId": on the DTO, StudentId is the user's Guid, so the
    // human-readable "IX-A-003" needs the other name to avoid colliding with it.
    [MapProperty("Student.StudentId", nameof(EnrollmentDto.StudentNumber))]
    [MapProperty("Class.Name", nameof(EnrollmentDto.ClassName))]
    [MapProperty("Class.Level", nameof(EnrollmentDto.ClassLevel))]
    [MapProperty("Class.Section", nameof(EnrollmentDto.ClassSection))]
    [MapProperty("AcademicYear.Name", nameof(EnrollmentDto.AcademicYearName))]
    public partial EnrollmentDto MapToDto(StudentEnrollment enrollment);

    [MapProperty(nameof(StudentEnrollment.Id), nameof(EnrolledClassDto.EnrollmentId))]
    [MapProperty("Class.Name", nameof(EnrolledClassDto.ClassName))]
    [MapProperty("Class.Level", nameof(EnrolledClassDto.ClassLevel))]
    [MapProperty("Class.Section", nameof(EnrolledClassDto.ClassSection))]
    [MapProperty("AcademicYear.Name", nameof(EnrolledClassDto.AcademicYearName))]
    public partial EnrolledClassDto MapToEnrolledClassDto(StudentEnrollment enrollment);
}
