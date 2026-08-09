using AssignmentSystem.Domain.AcademicYears;
using Riok.Mapperly.Abstractions;

namespace AssignmentSystem.Application.Features.AcademicYears;

[Mapper]
public partial class AcademicYearMapper
{
    // EnrollmentCount has no scalar source on AcademicYear — it is an aggregate the handler
    // fills in afterwards via IAcademicYearUsageReader, the same way ClassDto.StudentCount
    // is. Mapping it from the navigation collection would mean loading every enrollment row
    // just to take its length.
    [MapperIgnoreTarget(nameof(AcademicYearDto.EnrollmentCount))]
    public partial AcademicYearDto MapToDto(AcademicYear academicYear);
}
