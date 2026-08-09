using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Application.Features.Users;

/// <summary>
/// A user with their enrolled classes. Two levels deep because <c>UserDto.Classes</c>
/// carries class names, which live past the enrollment row.
/// </summary>
internal sealed class UserWithClassesByIdSpecification : Specification<ApplicationUser>
{
    public UserWithClassesByIdSpecification(Guid id)
    {
        Criteria = u => u.Id == id;
        AddInclude("Enrollments.Class");
    }
}

internal sealed class UsersPagedSpecification : Specification<ApplicationUser>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<ApplicationUser> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<ApplicationUser, object>>>
        {
            ["name"] = u => u.FullName,
            ["email"] = u => u.Email.Value,
            ["role"] = u => u.Role,
            ["createdAt"] = u => u.CreatedAtUtc,
        },
        tieBreaker: u => u.Id);

    public UsersPagedSpecification(
        IEnumerable<Domain.Enums.Role>? roles,
        IEnumerable<Guid>? classIds,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude("Enrollments.Class");
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderByDescending(u => u.CreatedAtUtc);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        var roleFilter = MultiValueFilter(roles);
        var classFilter = MultiValueFilter(classIds);

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
        //
        // The class filter is an EXISTS over enrollments now, not a column comparison —
        // a student in two classes correctly appears under either.
#pragma warning disable CA1304, CA1311
        Criteria = u =>
            (roleFilter == null || roleFilter.Contains(u.Role)) &&
            (classFilter == null || u.Enrollments.Any(e => classFilter.Contains(e.ClassId))) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             u.Email.Value.Contains(searchLower) ||
             u.FullName.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
