using Riok.Mapperly.Abstractions;
using AssignmentSystem.Domain.Submissions;

namespace AssignmentSystem.Application.Features.Submissions;

[Mapper]
public partial class SubmissionMapper
{
    [MapProperty("Assignment.Title", nameof(SubmissionDto.AssignmentTitle))]
    [MapProperty("Student.FullName", nameof(SubmissionDto.StudentName))]
    [MapProperty("ReviewedBy.FullName", nameof(SubmissionDto.ReviewedByName))]
    public partial SubmissionDto MapToDto(Submission submission);

    public partial SubmissionFileDto MapToFileDto(SubmissionFile file);
}
