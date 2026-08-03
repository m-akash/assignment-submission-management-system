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

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = u =>
            (!role.HasValue || u.Role == role.Value) &&
            (!classId.HasValue || u.ClassId == classId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             u.Email.Value.Contains(searchLower) ||
             u.FullName.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
