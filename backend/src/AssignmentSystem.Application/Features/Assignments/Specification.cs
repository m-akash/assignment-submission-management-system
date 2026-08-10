using System.Globalization;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Assignments;

/// <summary>
/// An assignment with everything the DTO and the authorization checks need. The offering
/// is included two levels deep because the class and course names live on the far side of
/// it, and because rule B1 needs the offering's class id to test enrollment.
/// </summary>
internal sealed class AssignmentWithDetailsSpecification : Specification<Assignment>
{
    public AssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude(a => a.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        AddInclude(a => a.Files);
    }
}

/// <summary>
/// The scope of an assignment without its files — for the write paths that need to answer
/// "which class and course is this for?" before they mutate something, and for composing
/// notification bodies.
/// </summary>
internal sealed class AssignmentWithScopeSpecification : Specification<Assignment>
{
    public AssignmentWithScopeSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
    }
}

internal sealed class AssignmentsPagedSpecification : Specification<Assignment>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<Assignment> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<Assignment, object>>>
        {
            ["title"] = a => a.Title,
            ["deadline"] = a => a.DeadlineUtc,
            ["maxMarks"] = a => a.MaxMarks,
            ["status"] = a => a.Status,
            ["createdAt"] = a => a.CreatedAtUtc,
        },
        tieBreaker: a => a.Id);

    public AssignmentsPagedSpecification(
        IEnumerable<Guid>? classIds,
        IEnumerable<Guid>? courseIds,
        IEnumerable<Guid>? classCourseIds,
        IReadOnlyList<Guid>? restrictToClassIds,
        IEnumerable<Guid>? teacherIds,
        IEnumerable<AssignmentStatus>? statuses,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(a => a.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        AddInclude(a => a.Files);
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderByDescending(a => a.CreatedAtUtc);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        var hasSearch = !string.IsNullOrWhiteSpace(searchLower);
        // The list shows numbers as well as text, and one search box has to reach all of it.
        // A whole number can be a grade or a submission count; anything that parses as a
        // decimal can be a mark. Each arm switches itself off when the term is not of its
        // shape, so "algebra" never reaches a numeric comparison.
        //
        // Invariant, not the server's culture: the term arrives as a query string typed into
        // a browser, where "77.5" is 77.5 whatever locale the machine happens to run in.
        var searchLevel = hasSearch && int.TryParse(searchLower, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLevel)
            ? parsedLevel
            : (int?)null;
        var searchNumber = hasSearch && decimal.TryParse(searchLower, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedNumber)
            ? parsedNumber
            : (decimal?)null;
        // Status is stored as an enum, so "pub" cannot be matched with a LIKE. The names are
        // resolved to values here instead and matched as an IN list. By prefix, not substring:
        // "d" should mean Draft, not every status whose name happens to contain a "d".
        var searchStatuses = hasSearch
            ? Enum.GetValues<AssignmentStatus>()
                .Where(status => status.ToString().StartsWith(searchLower!, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];
        var statusSearchFilter = searchStatuses.Count == 0 ? null : searchStatuses;

        var classFilter = MultiValueFilter(classIds);
        var courseFilter = MultiValueFilter(courseIds);
        var classCourseFilter = MultiValueFilter(classCourseIds);
        var teacherFilter = MultiValueFilter(teacherIds);
        var statusFilter = MultiValueFilter(statuses);

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
        //
        // restrictToClassIds is the student scope (rule B1): the classes they are enrolled
        // in. A student with no enrollment must see nothing, so an EMPTY list has to match
        // nothing — which is why it is used raw rather than through MultiValueFilter, whose
        // whole job is to turn an empty caller-supplied filter back into "no filter".
#pragma warning disable CA1304, CA1311
        Criteria = a =>
            (classCourseFilter == null || classCourseFilter.Contains(a.ClassCourseId)) &&
            (classFilter == null || classFilter.Contains(a.ClassCourse.ClassId)) &&
            (courseFilter == null || courseFilter.Contains(a.ClassCourse.CourseId)) &&
            (restrictToClassIds == null || restrictToClassIds.Contains(a.ClassCourse.ClassId)) &&
            (teacherFilter == null || teacherFilter.Contains(a.TeacherId)) &&
            (statusFilter == null || statusFilter.Contains(a.Status)) &&
            // One box, every column the list renders: the term is matched against the text
            // columns, and — where it is shaped like one — against the numeric and enum ones
            // too. The deadline is the one column left out: it is a timestamp rendered in the
            // reader's locale, so there is no stored text for a term to match against.
            (!hasSearch ||
             a.Title.ToLower().Contains(searchLower!) ||
             // DescriptionText, not Description: the description is markup, and matching
             // against it would turn a search for "li" into "every assignment containing a
             // list". The database keeps the stripped copy in step with the original.
             a.DescriptionText.ToLower().Contains(searchLower!) ||
             a.ClassCourse.Course.Name.ToLower().Contains(searchLower!) ||
             a.ClassCourse.Course.Code.ToLower().Contains(searchLower!) ||
             a.Teacher.FullName.ToLower().Contains(searchLower!) ||
             (searchLevel != null && a.ClassCourse.Class.Level == searchLevel) ||
             (searchLevel != null && a.SubmissionCount == searchLevel) ||
             (searchNumber != null && a.MaxMarks == searchNumber) ||
             (a.ClassCourse.Class.Section != null && a.ClassCourse.Class.Section.ToLower().Contains(searchLower!)) ||
             (statusSearchFilter != null && statusSearchFilter.Contains(a.Status)));
#pragma warning restore CA1304, CA1311
    }
}
