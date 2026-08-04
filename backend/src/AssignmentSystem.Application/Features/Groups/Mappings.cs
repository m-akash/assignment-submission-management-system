using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Groups;

namespace AssignmentSystem.Application.Features.Groups;

[Mapper]
public partial class GroupMapper
{
    public partial GroupDto MapToDto(Group group);
}
