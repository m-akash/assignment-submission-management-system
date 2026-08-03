using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Classes;

namespace AssignmentSystem.Application.Features.Classes;

[Mapper]
public partial class ClassMapper
{
    // StudentCount has no source property on Class — it's an aggregate the handler
    // fills in afterwards via IClassRosterRepository, not something Mapperly can map.
    [MapperIgnoreTarget(nameof(ClassDto.StudentCount))]
    public partial ClassDto MapToDto(Class classObj);
}
