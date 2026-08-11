using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// What the seeder actually produced, read straight from the database the API booted against.
/// The demo data is a deliverable in its own right — it is the first thing anyone evaluating this
/// project sees — so its shape is asserted rather than described in a README and hoped for.
///
/// Every query here is filtered to the seeded rows: the suite shares one database and the other
/// tests provision their own classes, courses and users as they go. Seeded classes are the only
/// ones in sections "A" and "B" (fixtures use a unique tag), seeded people are the only ones on
/// <c>@assignment.test</c> (fixtures use <c>@test.local</c>), and the seeded coursework is
/// identified by the titles <see cref="DemoCurriculum"/> defines — which is what keeps this test
/// from counting an assignment another test created as the demo teacher.
/// </summary>
public class SeededSchoolTests : IntegrationTestBase
{
    public SeededSchoolTests(ApiFactory api) : base(api) { }

    /// <summary>The titles the demo teacher's own offerings were seeded with — all 24 distinct.</summary>
    private static string[] SeededTitles() => DemoCurriculum.TeachingPlan
        .Where(p => p.TeacherIndex == DemoCurriculum.DemoTeacherIndex)
        .SelectMany(p => DemoCurriculum.BriefsFor(p.Subject, p.Level))
        .Select(b => b.Title)
        .ToArray();

    private static bool IsSeededSection(string? section) =>
        section is not null && DemoCurriculum.Sections.Contains(section);

    [Fact]
    public async Task EveryGradeStudiesItsOwnSubjects_AsSeparateCoursesCarryingTheGradeInTheCode()
    {
        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var classes = (await db.Classes.ToListAsync())
            .Where(c => IsSeededSection(c.Section))
            .ToList();

        classes.Should().HaveCount(DemoCurriculum.Levels.Length * DemoCurriculum.Sections.Length);
        classes.Select(c => c.Level).Distinct().OrderBy(l => l).Should().Equal(DemoCurriculum.Levels);

        var expectedCodes = DemoCurriculum.Subjects
            .SelectMany(s => s.Levels.Select(level => DemoCurriculum.CodeFor(s, level)))
            .ToArray();

        expectedCodes.Should().HaveCount(36).And.OnlyHaveUniqueItems();

        var codes = await db.Courses
            .Where(c => expectedCodes.Contains(c.Code))
            .Select(c => c.Code)
            .ToListAsync();

        codes.Should().BeEquivalentTo(expectedCodes, "one course per subject per grade that studies it");

        // The grade is readable off the code, which is the point of encoding it there.
        expectedCodes.Should().Contain(["BAN601", "ENG601", "GMATH601", "GSCI601", "BAN1101", "ENG1201", "HMATH901", "PHY1201"]);

        // And the stage boundary holds in both directions: no separate sciences below grade 9,
        // no combined science above it.
        expectedCodes.Should().NotContain(["PHY801", "CHE801", "BIO801", "HMATH801", "GSCI901", "GMATH901"]);

        // Every class studies its whole subject list — it is the teaching mapping that is
        // deliberately incomplete, never the curriculum.
        var classIds = classes.Select(c => c.Id).ToHashSet();
        var offerings = await db.ClassCourses.Where(o => classIds.Contains(o.ClassId)).ToListAsync();

        offerings.Should().HaveCount(72);
        foreach (var klass in classes)
        {
            offerings.Count(o => o.ClassId == klass.Id)
                .Should().Be(DemoCurriculum.SubjectsFor(klass.Level).Count(),
                    "class {0}{1} studies every subject its grade is given", klass.Level, klass.Section);
        }
    }

    [Fact]
    public async Task TheRosterIsFullButMostOfferingsAreStillWaitingForATeacher()
    {
        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Filtered in memory: the suite's user table is small, and `EndsWith` with an explicit
        // comparison has no SQL translation.
        var people = (await db.Users.ToListAsync())
            .Where(u => u.EmailValue.EndsWith("@assignment.test", StringComparison.Ordinal))
            .ToList();

        people.Count(u => u.Role == Role.Admin).Should().Be(1);
        people.Count(u => u.Role == Role.Teacher).Should().Be(7);
        people.Count(u => u.Role == Role.Student).Should().Be(70, "5 students in each of 14 sections");

        // Everybody is placed, and in the running session — coursework hangs off that year.
        var studentIds = people.Where(u => u.Role == Role.Student).Select(u => u.Id).ToHashSet();
        var enrollments = await db.StudentEnrollments.Where(e => studentIds.Contains(e.StudentId)).ToListAsync();
        var currentYear = await db.AcademicYears.SingleAsync(y => y.IsCurrent);

        enrollments.Should().HaveCount(70);
        enrollments.Should().OnlyContain(e => e.AcademicYearId == currentYear.Id);
        enrollments.Select(e => e.StudentId).Should().OnlyHaveUniqueItems("a student sits in one class per year");

        var classIds = (await db.Classes.ToListAsync())
            .Where(c => IsSeededSection(c.Section))
            .Select(c => c.Id)
            .ToHashSet();

        var offeringIds = await db.ClassCourses
            .Where(o => classIds.Contains(o.ClassId))
            .Select(o => o.Id)
            .ToListAsync();

        var mappings = await db.TeacherAssignments
            .Where(t => offeringIds.Contains(t.ClassCourseId))
            .ToListAsync();

        mappings.Should().HaveCount(28, "two mapped offerings per section, and no more");
        (offeringIds.Count - mappings.Count).Should().Be(44,
            "the rest are left blank so the admin's teacher-mapping screen has real work waiting");

        // Every teacher is actually teaching something — a seeded account with no classes would
        // be an empty login rather than a demonstration.
        var teacherIds = people.Where(u => u.Role == Role.Teacher).Select(u => u.Id);
        foreach (var teacherId in teacherIds)
        {
            mappings.Should().Contain(m => m.TeacherId == teacherId);
        }
    }

    [Fact]
    public async Task TheDemoTeachersCourseworkIsAuthored_PartlyPublished_AndAlwaysCarriesAnAttachment()
    {
        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var titles = SeededTitles();
        titles.Should().HaveCount(24).And.OnlyHaveUniqueItems();

        var demoTeacher = await db.Users.SingleAsync(u => u.Email.Value == DbSeeder.TeacherEmail);
        var assignments = await db.Assignments
            .Include(a => a.Files)
            .Where(a => a.TeacherId == demoTeacher.Id && titles.Contains(a.Title))
            .ToListAsync();

        assignments.Should().HaveCount(24, "three per offering the demo teacher holds");
        assignments.Count(a => a.Status == AssignmentStatus.Published).Should().Be(8, "one published per offering");
        assignments.Count(a => a.Status == AssignmentStatus.Draft).Should().Be(16);

        // The requirement that made the attachments necessary at all.
        assignments.Should().OnlyContain(a => a.Files.Count > 0);
        assignments.SelectMany(a => a.Files).Should().HaveCount(32,
            "a worksheet on every assignment, plus a figure on each published one");

        // The brief is a rich-text field, and a real one — not a single line of placeholder.
        foreach (var assignment in assignments)
        {
            var brief = Application.Common.Html.HtmlContent.ToPlainText(assignment.Description);
            brief.Length.Should().BeGreaterThan(400, "'{0}' needs a brief a student could work from", assignment.Title);
            assignment.Description.Should().Contain("<li>", "the tasks are a list, as the editor would have written them");
        }

        // Every published assignment has been handed in to by its whole class, and marked.
        var publishedIds = assignments
            .Where(a => a.Status == AssignmentStatus.Published)
            .Select(a => a.Id)
            .ToHashSet();

        var submissions = await db.Submissions
            .Include(s => s.Files)
            .Where(s => publishedIds.Contains(s.AssignmentId))
            .ToListAsync();

        submissions.Should().HaveCount(40, "5 students × 8 published assignments");
        submissions.Should().OnlyContain(s => s.Status == SubmissionStatus.Graded);
        submissions.Should().OnlyContain(s => s.Files.Count == 1, "each one was handed in with its work attached");
        submissions.Should().OnlyContain(s => s.Marks > 0 && s.Marks <= s.MarksOutOf);
        submissions.Should().OnlyContain(s => s.Feedback != null && s.ReviewedById == demoTeacher.Id);
        submissions.Should().OnlyContain(s => s.SubmittedAtUtc < s.ReviewedAtUtc, "marked after it was handed in");

        foreach (var assignmentId in publishedIds)
        {
            submissions.Count(s => s.AssignmentId == assignmentId).Should().Be(5);
        }

        // The cached count rule X6 relies on has to agree with the rows.
        foreach (var assignment in assignments.Where(a => publishedIds.Contains(a.Id)))
        {
            assignment.SubmissionCount.Should().Be(5);
        }
    }

    /// <summary>
    /// The demo student's own view. "Log in as the student and you see marked work" is the claim
    /// the README makes, and it only holds if the demo teacher happens to take that student's
    /// class — which is a property of the seeding plan, not of anything the domain enforces.
    /// </summary>
    [Fact]
    public async Task TheDemoStudentIsTaughtByTheDemoTeacherAndHasMarkedWorkWaiting()
    {
        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var student = await db.Users.SingleAsync(u => u.Email.Value == DbSeeder.StudentEmail);
        var demoTeacher = await db.Users.SingleAsync(u => u.Email.Value == DbSeeder.TeacherEmail);

        student.StudentId.Should().Be($"{DemoCurriculum.DemoStudentLevel}-{DemoCurriculum.DemoStudentSection}-001");

        var enrollment = await db.StudentEnrollments.SingleAsync(e => e.StudentId == student.Id);
        var klass = await db.Classes.SingleAsync(c => c.Id == enrollment.ClassId);
        klass.Level.Should().Be(DemoCurriculum.DemoStudentLevel);
        klass.Section.Should().Be(DemoCurriculum.DemoStudentSection);

        // The offerings of that class the demo teacher is mapped to.
        var taughtHere = await db.TeacherAssignments
            .Where(t => t.TeacherId == demoTeacher.Id)
            .Join(
                db.ClassCourses.Where(o => o.ClassId == klass.Id),
                mapping => mapping.ClassCourseId,
                offering => offering.Id,
                (mapping, offering) => offering.Id)
            .ToListAsync();

        taughtHere.Should().HaveCount(2, "both of this class's mapped offerings are the demo teacher's");

        var titles = SeededTitles();
        var visible = await db.Assignments
            .Include(a => a.Files)
            .Where(a => taughtHere.Contains(a.ClassCourseId) && titles.Contains(a.Title))
            .ToListAsync();

        visible.Should().HaveCount(6, "three per offering");
        visible.Count(a => a.Status == AssignmentStatus.Published).Should().Be(2);

        var published = visible.Where(a => a.Status == AssignmentStatus.Published).Select(a => a.Id).ToHashSet();
        var own = await db.Submissions
            .Include(s => s.Files)
            .Where(s => s.StudentId == student.Id && published.Contains(s.AssignmentId))
            .ToListAsync();

        own.Should().HaveCount(2);
        own.Should().OnlyContain(s => s.Status == SubmissionStatus.Graded && s.Marks > 0);
        own.Should().OnlyContain(s => s.Files.Count == 1);
    }

    /// <summary>
    /// The attachments are the part of the seed that lives outside the database, so a row pointing
    /// at nothing would look perfectly healthy in every query above. This opens each one through
    /// the storage abstraction the download endpoint uses and checks the bytes are the file the row
    /// claims: the right length, and a header matching the content type it will be served with.
    /// </summary>
    [Fact]
    public async Task EverySeededAttachment_IsReadableFromStorageAndIsTheFileItsRowClaims()
    {
        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var titles = SeededTitles();
        var demoTeacher = await db.Users.SingleAsync(u => u.Email.Value == DbSeeder.TeacherEmail);

        var assignmentIds = await db.Assignments
            .Where(a => a.TeacherId == demoTeacher.Id && titles.Contains(a.Title))
            .Select(a => a.Id)
            .ToListAsync();

        var attachments = new List<(string Name, string ContentType, long Size, string Path)>();

        attachments.AddRange(await db.AssignmentFiles
            .Where(f => assignmentIds.Contains(f.AssignmentId))
            .Select(f => new ValueTuple<string, string, long, string>(
                f.OriginalFileName, f.ContentType, f.FileSizeBytes, f.RelativePath))
            .ToListAsync());

        attachments.AddRange(await db.SubmissionFiles
            .Where(f => assignmentIds.Contains(f.Submission.AssignmentId))
            .Select(f => new ValueTuple<string, string, long, string>(
                f.OriginalFileName, f.ContentType, f.FileSizeBytes, f.RelativePath))
            .ToListAsync());

        attachments.Should().HaveCount(72, "32 on assignments and 40 on submissions");

        foreach (var (name, contentType, size, path) in attachments)
        {
            size.Should().BeGreaterThan(0);

            using var stream = storage.OpenRead(path);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            var bytes = buffer.ToArray();

            bytes.Should().HaveCount((int)size, "{0} must be the size its row records", name);

            switch (contentType)
            {
                case "application/pdf":
                    bytes.Take(4).Should().Equal([0x25, 0x50, 0x44, 0x46], "{0} must really be a PDF", name);
                    break;

                case "image/png":
                    bytes.Take(8).Should().Equal(
                        [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A],
                        "{0} must really be a PNG", name);
                    break;

                case "text/plain":
                    bytes.Take(8).Should().NotContain((byte)0x00, "{0} must really be text", name);
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected seeded content type '{contentType}' on {name}.");
            }
        }

        // All three kinds the in-app viewer can render are present, which is the reason the seed
        // writes more than one format.
        attachments.Select(a => a.ContentType).Distinct()
            .Should().BeEquivalentTo(["application/pdf", "image/png", "text/plain"]);
    }
}
