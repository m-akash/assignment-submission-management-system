using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.Assignments;

/// <summary>
/// An assignment created by a teacher for one course offering.
/// Carries title, description, deadline, max marks, draft/published status, and
/// a resubmission flag. Soft-deletable. Enforces:
///  - draft → published is one-way (rule B6)
///  - metadata edits blocked once it has submissions (rule X6)
/// </summary>
public sealed class Assignment : BaseEntity, ISoftDeletable
{
    /// <summary>
    /// The offering this assignment belongs to — one column carrying both the class and
    /// the course, so the pair can never contradict itself. Read the class or course
    /// through <c>ClassCourse</c>.
    /// </summary>
    public Guid ClassCourseId { get; private set; }
    public ClassCourse ClassCourse { get; private set; } = null!;

    /// <summary>
    /// The authoring teacher. Stored here rather than reached through the teaching link
    /// so ownership checks (rule B3) are a column comparison, and so removing an admin's
    /// teacher↔offering mapping cannot orphan the authorship of work already set.
    /// </summary>
    public Guid TeacherId { get; private set; }
    public ApplicationUser Teacher { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    /// <summary>
    /// The brief, as sanitized HTML — authored in the client's rich-text editor and reduced
    /// to an allowlist before it ever reaches this property.
    /// </summary>
    public string Description { get; private set; } = null!;

    /// <summary>
    /// <see cref="Description"/> with its tags stripped, so searching for a word in a brief
    /// finds the brief rather than every assignment whose markup happens to contain the
    /// letters. Never assigned here: it is a stored column the database generates from
    /// <see cref="Description"/>, which is what makes it impossible for the two to disagree
    /// and why rows written before the editor existed are covered without a backfill.
    /// </summary>
    public string DescriptionText { get; private set; } = null!;

    /// <summary>Stored in UTC; comparisons against "now" must use UTC.</summary>
    public DateTime DeadlineUtc { get; private set; }
    public decimal MaxMarks { get; private set; }

    public AssignmentStatus Status { get; private set; } = AssignmentStatus.Draft;

    /// <summary>Whether a student may update their submission after first submit.</summary>
    public bool AllowResubmission { get; private set; } = true;

    // count kept in sync for rule X6 enforcement (handler increments via HasSubmissions flag)
    public int SubmissionCount { get; private set; }

    // ── Soft delete ───────────────────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    // navigation
    private readonly List<Submissions.Submission> _submissions = [];
    public IReadOnlyCollection<Submissions.Submission> Submissions => _submissions.AsReadOnly();

    private readonly List<AssignmentFile> _files = [];
    public IReadOnlyCollection<AssignmentFile> Files => _files.AsReadOnly();

    private Assignment() { }

    public static Assignment Create(
        Guid teacherId,
        Guid classCourseId,
        string title,
        string description,
        DateTime deadlineUtc,
        decimal maxMarks,
        bool allowResubmission,
        IClock clock)
    {
        if (teacherId == Guid.Empty || classCourseId == Guid.Empty)
        {
            throw new DomainException("Teacher and class-course ids are both required.");
        }

        ValidateCommon(title, description, deadlineUtc, maxMarks, clock);

        return new Assignment
        {
            TeacherId = teacherId,
            ClassCourseId = classCourseId,
            Title = title.Trim(),
            Description = description.Trim(),
            DeadlineUtc = deadlineUtc,
            MaxMarks = Math.Round(maxMarks, 2),
            AllowResubmission = allowResubmission,
            Status = AssignmentStatus.Draft,
        };
    }

    /// <summary>
    /// Updates editable metadata. Once a published assignment has submissions only
    /// the description may be extended (rule X6).
    /// </summary>
    public void Update(
        string title,
        string description,
        DateTime deadlineUtc,
        decimal maxMarks,
        bool allowResubmission,
        IClock clock,
        bool hasSubmissions)
    {
        if (Status == AssignmentStatus.Published && hasSubmissions)
        {
            // X6: only description may change once there are submissions against a published assignment.
            // Allow up to 1-minute tolerance for deadlineUtc to handle client-side datetime-local precision loss.
            var isDeadlineChanged = Math.Abs((DeadlineUtc - deadlineUtc).TotalMinutes) >= 1;

            if (!string.Equals(Title.Trim(), title.Trim(), StringComparison.Ordinal)
                || isDeadlineChanged
                || Math.Round(maxMarks, 2) != MaxMarks
                || AllowResubmission != allowResubmission)
            {
                throw new DomainException(
                    "Title, deadline, max marks and resubmission flag cannot be changed once a published assignment has submissions.");
            }

            Description = description.Trim();
            return;
        }

        ValidateCommon(title, description, deadlineUtc, maxMarks, clock);

        Title = title.Trim();
        Description = description.Trim();
        DeadlineUtc = deadlineUtc;
        MaxMarks = Math.Round(maxMarks, 2);
        AllowResubmission = allowResubmission;
    }

    /// <summary>Publishes a draft assignment (one-way, rule B6).</summary>
    public void Publish()
    {
        if (Status == AssignmentStatus.Published)
        {
            throw new DomainException("Assignment is already published.");
        }

        Status = AssignmentStatus.Published;
    }

    /// <summary>True when the deadline has passed (computed against the given clock).</summary>
    public bool IsPastDeadline(IClock clock) => clock.UtcNow >= DeadlineUtc;

    /// <summary>True when the caller is the owning teacher (rule B3).</summary>
    public bool IsOwnedBy(Guid teacherId) => TeacherId == teacherId;

    /// <summary>Incremented by the handler when a submission is created (for X6 checks).</summary>
    public void IncrementSubmissionCount() => SubmissionCount++;

    public void AttachFile(AssignmentFile file)
    {
        if (file.AssignmentId != Id)
        {
            throw new DomainException("File does not belong to this assignment.");
        }

        _files.Add(file);
    }

    private static void ValidateCommon(string title, string description, DateTime deadlineUtc, decimal maxMarks, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Assignment title is required.");
        }

        if (title.Length > 200)
        {
            throw new DomainException("Assignment title cannot exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Assignment description is required.");
        }

        if (maxMarks <= 0)
        {
            throw new DomainException("Maximum marks must be greater than zero.");
        }

        // rule X5: deadline must be at least 1 hour in the future.
        if (deadlineUtc <= clock.UtcNow.AddHours(1))
        {
            throw new DomainException("Assignment deadline must be at least 1 hour from now.");
        }
    }

    public void SoftDelete(DateTime deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
    }
}
