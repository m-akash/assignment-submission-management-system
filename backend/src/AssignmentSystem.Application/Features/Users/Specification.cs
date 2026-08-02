using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Application.Features.Users;

internal sealed class UserWithClassByIdSpecification : Specification<ApplicationUser>
{
    public UserWithClassByIdSpecification(Guid id)
    {
        Criteria = u => u.Id == id;
        AddInclude(u => u.Class!);
    }
}

internal sealed class UsersPagedSpecification : Specification<ApplicationUser>
{
    public UsersPagedSpecification(
        Domain.Enums.Role? role,
        Guid? classId,
        string? search,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(u => u.Class!);
        ApplyOrderByDescending(u => u.CreatedAtUtc);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        Criteria = u =>
            (!role.HasValue || u.Role == role.Value) &&
            (!classId.HasValue || u.ClassId == classId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             u.Email.Value.Contains(searchLower) ||
             u.FullName.ToLowerInvariant().Contains(searchLower));
    }
}
