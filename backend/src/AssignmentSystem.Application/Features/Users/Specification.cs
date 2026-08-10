using System.Globalization;
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
        AddInclude("Enrollments.AcademicYear");
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
        AddInclude("Enrollments.AcademicYear");
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderByDescending(u => u.CreatedAtUtc);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        var hasSearch = !string.IsNullOrWhiteSpace(searchLower);
        // A whole number is a grade ("9"); anything else can only be text. Parsed once here
        // so the grade arm switches itself off. Invariant, not the server's culture: the term
        // arrives as a query string typed into a browser.
        var searchLevel = hasSearch && int.TryParse(searchLower, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLevel)
            ? parsedLevel
            : (int?)null;
        // The role is stored as an enum, so "teach" cannot be matched with a LIKE. The names
        // are resolved to values here and matched as an IN list. By prefix, not substring:
        // "a" should mean Admin, not every role whose name happens to contain an "a".
        var searchRoles = hasSearch
            ? Enum.GetValues<Domain.Enums.Role>()
                .Where(role => role.ToString().StartsWith(searchLower!, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];
        var roleSearchFilter = searchRoles.Count == 0 ? null : searchRoles;
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
            // One box, every column the list shows. The school id ("8-B-001") and the
            // grade/section/session of an enrollment are columns too, and a name-and-email
            // search could not reach any of them. The enrollment arms are an EXISTS, like the
            // class filter above: the row lists a line per enrollment, so matching any one of
            // them is what matches the row a reader is looking at.
            (!hasSearch ||
             u.Email.Value.Contains(searchLower!) ||
             u.FullName.ToLower().Contains(searchLower!) ||
             (u.StudentId != null && u.StudentId.ToLower().Contains(searchLower!)) ||
             (u.TeacherId != null && u.TeacherId.ToLower().Contains(searchLower!)) ||
             (roleSearchFilter != null && roleSearchFilter.Contains(u.Role)) ||
             u.Enrollments.Any(e =>
                 (searchLevel != null && e.Class.Level == searchLevel) ||
                 (e.Class.Section != null && e.Class.Section.ToLower().Contains(searchLower!)) ||
                 e.AcademicYear.Name.ToLower().Contains(searchLower!)));
#pragma warning restore CA1304, CA1311
    }
}
