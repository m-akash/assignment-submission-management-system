using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Application.Features.Users;

[Mapper]
public partial class UserMapper
{
    [MapProperty(nameof(ApplicationUser.EmailValue), nameof(UserDto.Email))]
    [MapProperty("Class.Name", nameof(UserDto.ClassName))]
    public partial UserDto MapToDto(ApplicationUser user);
}
