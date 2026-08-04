using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Assignments;

namespace AssignmentSystem.Application.Features.AssignmentFiles;

[Mapper]
public partial class AssignmentFileMapper
{
    public partial AssignmentFileDto MapToDto(AssignmentFile file);
}
