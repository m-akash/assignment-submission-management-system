using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Departments;

namespace AssignmentSystem.Application.Features.Departments;

[Mapper]
public partial class DepartmentMapper
{
    public partial DepartmentDto MapToDto(Department department);
}
