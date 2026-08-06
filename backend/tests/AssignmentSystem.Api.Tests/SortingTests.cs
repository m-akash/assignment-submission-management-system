using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Sorting on the list endpoints.
///
/// The allow-list is the point of most of these: <c>sortBy</c> is caller-supplied text, and an
/// endpoint that turned it into a property access would be one query string away from ordering
/// by a column that is nobody's business — or from a 500 on a name that does not exist.
/// </summary>
public sealed class SortingTests : IntegrationTestBase
{
    public SortingTests(ApiFactory api) : base(api) { }

    private sealed record CourseRow(Guid Id, string Name, string Code);

    private async Task<List<CourseRow>> CoursesAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/v1/courses{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (items, _) = await ReadPageAsync<CourseRow>(response);
        return items;
    }

    [Fact]
    public async Task AListEndpoint_SortsAscendingByARequestedColumn()
    {
        using var admin = await SignInAsAdminAsync();

        var names = (await CoursesAsync(admin, "?sortBy=name&sortDir=asc&pageSize=50"))
            .Select(c => c.Name).ToList();

        names.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task AListEndpoint_SortsDescendingWhenAsked()
    {
        using var admin = await SignInAsAdminAsync();

        var names = (await CoursesAsync(admin, "?sortBy=name&sortDir=desc&pageSize=50"))
            .Select(c => c.Name).ToList();

        names.Should().BeInDescendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task ADifferentColumn_ProducesADifferentOrder()
    {
        using var admin = await SignInAsAdminAsync();

        var byName = await CoursesAsync(admin, "?sortBy=name&pageSize=50");
        var byCode = await CoursesAsync(admin, "?sortBy=code&pageSize=50");

        byCode.Select(c => c.Code).Should().BeInAscendingOrder(StringComparer.Ordinal);
        byName.Should().HaveCount(byCode.Count, "sorting changes order, never membership");
    }

    /// <summary>
    /// An unknown key must not fail the request and must not be interpreted. Falling back to
    /// the endpoint's natural order keeps a stale client working instead of breaking it.
    /// </summary>
    [Theory]
    [InlineData("passwordHash")]
    [InlineData("Id; DROP TABLE users")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    public async Task AnUnknownOrHostileSortKey_FallsBackToTheNaturalOrder(string sortBy)
    {
        using var admin = await SignInAsAdminAsync();

        var requested = await CoursesAsync(admin, $"?sortBy={Uri.EscapeDataString(sortBy)}&pageSize=50");
        var natural = await CoursesAsync(admin, "?pageSize=50");

        requested.Select(c => c.Id).Should().Equal(natural.Select(c => c.Id));
    }

    [Fact]
    public async Task AnUnrecognisedDirection_IsTreatedAsAscending()
    {
        using var admin = await SignInAsAdminAsync();

        var odd = await CoursesAsync(admin, "?sortBy=name&sortDir=sideways&pageSize=50");
        var ascending = await CoursesAsync(admin, "?sortBy=name&sortDir=asc&pageSize=50");

        odd.Select(c => c.Id).Should().Equal(ascending.Select(c => c.Id));
    }

    /// <summary>
    /// Sorting has to survive paging. Without a tiebreaker on a non-unique column, rows with
    /// equal values can come back in a different order per query — and a row then appears on
    /// two pages, or on none.
    /// </summary>
    [Fact]
    public async Task PagingASortedList_VisitsEachRowExactlyOnce()
    {
        using var admin = await SignInAsAdminAsync();

        var all = await CoursesAsync(admin, "?sortBy=name&pageSize=100");
        all.Count.Should().BeGreaterThan(3, "the seeded data needs to span more than one page");

        const int PageSize = 2;
        var paged = new List<Guid>();
        for (var page = 1; paged.Count < all.Count; page++)
        {
            var slice = await CoursesAsync(admin, $"?sortBy=name&page={page}&pageSize={PageSize}");
            if (slice.Count == 0)
            {
                break;
            }
            paged.AddRange(slice.Select(c => c.Id));
        }

        paged.Should().Equal(all.Select(c => c.Id), "paging a sorted list is just that list, in slices");
        paged.Should().OnlyHaveUniqueItems();
    }

    /// <summary>Sorting is a read concern; it must not widen what a caller can see.</summary>
    [Fact]
    public async Task SortingDoesNotBypassRoleScoping()
    {
        var world = await ProvisionWorldAsync("sort");
        using var student = await SignInAsync(world.StudentEmail);

        var response = await student.GetAsync("/api/v1/users?sortBy=email&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the user list is admin-only, sorted or not");
    }
}
