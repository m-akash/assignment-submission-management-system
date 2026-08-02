using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Classes;

namespace AssignmentSystem.Application.Features.Classes;

[Mapper]
public partial class ClassMapper
{
    public partial ClassDto MapToDto(Class classObj);
}
