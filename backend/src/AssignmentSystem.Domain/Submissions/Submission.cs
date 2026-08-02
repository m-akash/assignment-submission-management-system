using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.Submissions;

/// <summary>
/// A student's submission for an assignment. One per (assignment, student) — enforced
/// at the DB level via a unique index (rule X4). Encodes:
///  - can only be edited before the deadline (rule B2)
///  - submitting after the deadline marks it Late (rule X2)
///  - can't submit to a draft assignment (rule X3)
///  - grading requires a published assignment and marks ≤ max (rules B4, B7, X1)
///  - feedback length limit (rule X5)
/// </summary>
public sealed class Submission : BaseEntity
{
    public Guid AssignmentId { get; private set; }
    public Assignment Assignment { get; private set; } = null!;

    public Guid StudentId { get; private set; }
    public ApplicationUser Student { get; private set; } = null!;

    /// <summary>Text answer; nullable when only a file is attached.</summary>
    public string? Content { get; private set; }

    public SubmissionStatus Status { get; private set; } = SubmissionStatus.Pending;

    public DateTime? SubmittedAtUtc { get; private set; }

    // ── Grading ───────────────────────────────────────────────────────────────
    public decimal? Marks { get; private set; }
    public decimal? MarksOutOf { get; private set; }
    public string? Feedback { get; private set; }
    public Guid? ReviewedById { get; private set; }
    public ApplicationUser? ReviewedBy { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }

    // ── Files ─────────────────────────────────────────────────────────────────
    private readonly List<SubmissionFile> _files = [];
    public IReadOnlyCollection<SubmissionFile> Files => _files.AsReadOnly();

    private Submission() { }

    /// <summary>
    /// Creates a submission. The caller (handler) must have already verified the
    /// student belongs to the assignment's class (rule B1) and that the assignment
    /// is published (rule X3). Sets Late if the deadline has passed (rule X2).
    /// </summary>
    public static Submission Create(
        Guid assignmentId,
        Guid studentId,
        string? content,
        bool hasFile,
        Assignment assignment,
        IClock clock)
    {
        if (assignment.Status != AssignmentStatus.Published)
        {
            throw new DomainException("Cannot submit to an unpublished assignment.");
        }

        if (!HasContent(content, hasFile))
        {
            throw new DomainException("A submission must include a text answer or a file.");
        }

        var now = clock.UtcNow;
        var pastDeadline = now >= assignment.DeadlineUtc;

        return new Submission
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            Content = NormalizeContent(content),
            Status = pastDeadline ? SubmissionStatus.Late : SubmissionStatus.Pending,
            SubmittedAtUtc = now,
            MarksOutOf = assignment.MaxMarks,
        };
    }

    /// <summary>
    /// Updates the text answer before the deadline (rule B2). Refuses once the
    /// deadline has passed unless the assignment allows resubmission. A Late
    /// submission cannot be edited (rule X2).
    /// </summary>
    public void UpdateContent(string? content, bool hasFile, bool allowResubmission, DateTime deadlineUtc, IClock clock)
    {
        if (Status == SubmissionStatus.Graded)
        {
            throw new DomainException("Cannot edit a submission that has already been graded.");
        }

        if (Status == SubmissionStatus.Late)
        {
            throw new DomainException("Cannot edit a late submission after the deadline.");
        }

        var now = clock.UtcNow;
        if (now >= deadlineUtc)
        {
            // past deadline: only permitted when resubmission is explicitly allowed.
            if (!allowResubmission)
            {
                throw new DomainException("Cannot update a submission after the deadline.");
            }

            Status = SubmissionStatus.Late;
        }

        if (!HasContent(content, hasFile))
        {
            throw new DomainException("A submission must include a text answer or a file.");
        }

        Content = NormalizeContent(content);
        SubmittedAtUtc = now;
    }

    /// <summary>
    /// Grades the submission (rules B4, B7, X1). Requires a published assignment.
    /// Marks are bounded by the assignment maximum; the teacher must own the assignment.
    /// </summary>
    public void Grade(decimal marks, string? feedback, Guid reviewedBy, Assignment assignment, IClock clock)
    {
        if (assignment.Status != AssignmentStatus.Published)
        {
            throw new DomainException("Cannot grade an unpublished assignment.");
        }

        // Throws DomainException if marks are negative or exceed the maximum (rules B4, X5).
        // (Qualify the Marks value-object type — it is shadowed here by the Marks property.)
        var grade = Domain.Submissions.Marks.Create(marks, assignment.MaxMarks);

        if (feedback is { Length: > 2000 })
        {
            throw new DomainException("Feedback cannot exceed 2000 characters.");
        }

        Marks = grade.Value;
        MarksOutOf = grade.OutOf;
        Feedback = feedback;
        ReviewedById = reviewedBy;
        ReviewedAtUtc = clock.UtcNow;
        Status = SubmissionStatus.Graded;
    }

    /// <summary>
    /// Lets a teacher change the submission status manually (rule B7 —
    /// "change the submission status when necessary"). Allows moving back to Pending
    /// for re-evaluation but not to Late (that is deadline-derived).
    /// </summary>
    public void SetStatus(SubmissionStatus newStatus, Guid reviewedBy, IClock clock)
    {
        if (newStatus == SubmissionStatus.Late)
        {
            throw new DomainException("Late status is set automatically based on the deadline.");
        }

        Status = newStatus;
        ReviewedById = reviewedBy;
        if (newStatus == SubmissionStatus.Graded)
        {
            ReviewedAtUtc = clock.UtcNow;
        }
    }

    public void AttachFile(SubmissionFile file)
    {
        if (file.SubmissionId != Id)
        {
            throw new DomainException("File does not belong to this submission.");
        }

        _files.Add(file);
    }

    public bool IsOwnedBy(Guid studentId) => StudentId == studentId;

    private static bool HasContent(string? content, bool hasFile) =>
        !string.IsNullOrWhiteSpace(content) || hasFile;

    private static string? NormalizeContent(string? content) =>
        string.IsNullOrWhiteSpace(content) ? null : content.Trim();
}
