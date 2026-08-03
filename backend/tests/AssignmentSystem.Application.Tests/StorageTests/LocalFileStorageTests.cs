using System;
using System.IO;
using System.Threading.Tasks;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AssignmentSystem.Application.Tests.StorageTests;

/// <summary>
/// The storage backend's two safety properties: nothing resolves outside the storage
/// root, and no stream may exceed the configured size.
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "asm-storage-tests", Guid.NewGuid().ToString("N"));

    private LocalFileStorage Storage(long maxBytes = 1024) =>
        new(Options.Create(new FileStorageOptions { Root = _root, MaxBytes = maxBytes }));

    [Fact]
    public async Task SaveAsync_ShouldWriteInsideTheRootAndReportTheRealSize()
    {
        var bytes = "hello"u8.ToArray();

        var saved = await Storage().SaveAsync(new MemoryStream(bytes), ".txt");

        saved.SizeBytes.Should().Be(bytes.Length);
        saved.RelativePath.Should().NotStartWith("/").And.Contain("/");
        File.Exists(Path.Combine(_root, saved.RelativePath)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ShouldRoundTripThroughOpenRead()
    {
        var bytes = "round trip"u8.ToArray();
        var storage = Storage();

        var saved = await storage.SaveAsync(new MemoryStream(bytes), ".txt");

        await using var read = storage.OpenRead(saved.RelativePath);
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);
        buffer.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task SaveAsync_OverTheLimit_ShouldThrowAndLeaveNoPartialFile()
    {
        var storage = Storage(maxBytes: 16);
        var oversized = new byte[64];

        var act = async () => await storage.SaveAsync(new MemoryStream(oversized), ".txt");

        await act.Should().ThrowAsync<FileTooLargeException>();

        // A half-written file would silently consume the volume.
        Directory.Exists(_root).Should().BeTrue();
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\win.ini")]
    [InlineData("2026/08/../../../outside.txt")]
    public void OpenRead_WithTraversalPath_ShouldBeDenied(string relativePath)
    {
        var act = () => Storage().OpenRead(relativePath);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void OpenRead_WithAbsolutePath_ShouldBeDenied()
    {
        var absolute = OperatingSystem.IsWindows() ? @"C:\Windows\win.ini" : "/etc/passwd";

        var act = () => Storage().OpenRead(absolute);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    /// <summary>
    /// A prefix-only check would accept a sibling directory whose name merely starts with
    /// the root — "<c>{root}-evil</c>" sits outside the root but shares its first characters.
    /// </summary>
    [Fact]
    public void OpenRead_WithSiblingDirectorySharingTheRootPrefix_ShouldBeDenied()
    {
        var rootName = new DirectoryInfo(_root).Name;

        var act = () => Storage().OpenRead($"../{rootName}-evil/secret.txt");

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Delete_WithTraversalPath_ShouldBeDenied()
    {
        var act = () => Storage().Delete("../../../important.txt");

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Delete_WithMissingFile_ShouldBeANoOp()
    {
        var act = () => Storage().Delete("2026/08/not-there.txt");

        act.Should().NotThrow();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
