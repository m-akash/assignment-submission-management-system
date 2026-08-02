using System.Security.Claims;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Authentication;

namespace AssignmentSystem.Api.Authentication;

/// <summary>
/// Resolves the authenticated principal from <see cref="HttpContext"/> into an
/// application identity. Handlers depend on this — never on client-supplied user ids.
/// </summary>
internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var sub = Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
        ?? Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public string? FullName =>
        Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name)?.Value
        ?? Principal?.FindFirst(ClaimTypes.Name)?.Value;

    public Role? Role
    {
        get
        {
            var role = Principal?.FindFirst(CustomClaims.Role)?.Value
                       ?? Principal?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<Role>(role, out var parsed) ? parsed : null;
        }
    }

    public Guid? ClassId
    {
        get
        {
            var classId = Principal?.FindFirst(CustomClaims.ClassId)?.Value;
            return Guid.TryParse(classId, out var id) && id != Guid.Empty ? id : null;
        }
    }

    public bool IsInRole(Role role) => Role == role;
}
