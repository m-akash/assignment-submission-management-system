using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Subjects;

namespace AssignmentSystem.Application.Features.Subjects;

[Mapper]
public partial class SubjectMapper
{
    public partial SubjectDto MapToDto(Subject subject);
}
