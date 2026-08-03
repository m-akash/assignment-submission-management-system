using System;
using System.IO;
using System.Linq;
using AssignmentSystem.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AssignmentSystem.Application.Tests.StorageTests;

/// <summary>
/// Upload validation in isolation: the allow-list comes from configuration, and the
/// bytes are checked against it — a claimed extension or content type is never enough.
/// </summary>
public class FileUploadPolicyTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];
    private static readonly byte[] Docx = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Windows = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    private static FileUploadPolicy Policy(
        long maxBytes = 1024,
        int maxFiles = 3,
        params string[] allowed)
    {
        var options = new FileStorageOptions
        {
            Root = "./_test",
            MaxBytes = maxBytes,
            MaxFilesPerSubmission = maxFiles,
            AllowedExtensions = allowed.Length > 0 ? allowed : ["pdf", "docx", "txt", "png"],
        };

        return new FileUploadPolicy(Options.Create(options));
    }

    [Fact]
    public void Validate_WithMatchingPdf_ShouldSucceedAndDeriveContentType()
    {
        var result = Policy().Validate("essay.pdf", Pdf.Length, new MemoryStream(Pdf));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Extension.Should().Be(".pdf");
        result.Value.ContentType.Should().Be("application/pdf");
    }

    [Theory]
    [InlineData("report.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("chart.png", "image/png")]
    public void Validate_ShouldDeriveContentTypeFromTheVerifiedExtension(string fileName, string expected)
    {
        var bytes = fileName.EndsWith(".docx", StringComparison.Ordinal) ? Docx : Png;

        var result = Policy().Validate(fileName, bytes.Length, new MemoryStream(bytes));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContentType.Should().Be(expected);
    }

    [Fact]
    public void Validate_ShouldNormalizeExtensionCasing()
    {
        var result = Policy().Validate("ESSAY.PDF", Pdf.Length, new MemoryStream(Pdf));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Extension.Should().Be(".pdf");
    }

    [Fact]
    public void Validate_WithExtensionOutsideTheConfiguredList_ShouldFail()
    {
        // .png is a real image, but this deployment does not permit it.
        var result = Policy(allowed: ["pdf", "txt"]).Validate("chart.png", Png.Length, new MemoryStream(Png));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SubmissionFile.InvalidExtension");
    }

    [Fact]
    public void Validate_ShouldHonourAnExtendedConfiguredList()
    {
        var result = Policy(allowed: ["png"]).Validate("chart.png", Png.Length, new MemoryStream(Png));

        result.IsSuccess.Should().BeTrue("the allow-list is configuration, not a hard-coded constant");
    }

    [Fact]
    public void Validate_WithNoExtension_ShouldFail()
    {
        var result = Policy().Validate("essay", Pdf.Length, new MemoryStream(Pdf));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SubmissionFile.InvalidExtension");
    }

    [Fact]
    public void Validate_WhenSignatureDoesNotMatchExtension_ShouldFail()
    {
        // A .docx (ZIP) payload wearing a .pdf name.
        var result = Policy().Validate("disguised.pdf", Docx.Length, new MemoryStream(Docx));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SubmissionFile.InvalidContent");
    }

    /// <summary>
    /// The gap worth closing: plain text has no signature, so an "anything goes" rule made
    /// <c>payload.exe → notes.txt</c> a valid upload. A NUL byte marks it as binary.
    /// </summary>
    [Fact]
    public void Validate_WithBinaryContentRenamedToTxt_ShouldFail()
    {
        var result = Policy().Validate("notes.txt", Windows.Length, new MemoryStream(Windows));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SubmissionFile.InvalidContent");
    }

    [Fact]
    public void Validate_WithRealTextAsTxt_ShouldSucceed()
    {
        var bytes = "My answer to question one.\r\nAnd question two."u8.ToArray();

        var result = Policy().Validate("notes.txt", bytes.Length, new MemoryStream(bytes));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContentType.Should().Be("text/plain");
    }

    [Fact]
    public void Validate_OverTheSizeLimit_ShouldFailWithTheLimitInTheMessage()
    {
        var policy = Policy(maxBytes: 2 * 1024 * 1024);

        var result = policy.Validate("essay.pdf", (2 * 1024 * 1024) + 1, new MemoryStream(Pdf));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SubmissionFile.TooLarge");
        result.Error.Message.Should().Contain("2 MB");
    }

    [Fact]
    public void Validate_AtExactlyTheSizeLimit_ShouldSucceed()
    {
        var bytes = new byte[512];
        Pdf.CopyTo(bytes, 0);

        var result = Policy(maxBytes: 512).Validate("essay.pdf", 512, new MemoryStream(bytes));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyFile_ShouldFail()
    {
        var result = Policy().Validate("essay.pdf", 0, new MemoryStream());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SubmissionFile.Empty");
    }

    [Fact]
    public void Validate_ShouldLeaveTheStreamPositionUntouchedForTheCaller()
    {
        var stream = new MemoryStream(Pdf);

        Policy().Validate("essay.pdf", Pdf.Length, stream);

        stream.Position.Should().Be(0, "the handler still has to store these bytes");
    }

    [Fact]
    public void Policy_ShouldExposeConfiguredLimits()
    {
        var policy = Policy(maxBytes: 4096, maxFiles: 2, allowed: ["pdf", ".TXT"]);

        policy.MaxBytes.Should().Be(4096);
        policy.MaxFilesPerSubmission.Should().Be(2);
        policy.AllowedExtensions.Should().BeEquivalentTo([".pdf", ".txt"], "entries are normalized");
    }
}
