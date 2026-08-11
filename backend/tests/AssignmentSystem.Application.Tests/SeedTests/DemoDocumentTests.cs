using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using AssignmentSystem.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AssignmentSystem.Application.Tests.SeedTests;

/// <summary>
/// The seeder's attachments are hand-written file formats, which is only acceptable while
/// something proves they are real files. So these tests read the bytes back the way a consumer
/// would: the app's own upload policy checks the signatures, the PDF's cross-reference table is
/// re-parsed and every offset followed, and the PNG's chunks are walked, CRC-checked and
/// inflated.
///
/// The offsets are the part worth guarding. A PDF whose xref is out by a byte still looks
/// plausible in a hex dump and still starts with %PDF, but no viewer will open it — and the
/// failure would surface as "the demo attachment is broken", long after the change that caused it.
/// </summary>
public class DemoDocumentTests
{
    private static readonly string[] Tasks =
    [
        "Solve the fifteen equations in Section A, showing every step and checking each root by substitution.",
        "Form and solve an equation for each of the five word problems in Section C.",
        "Explain in two or three sentences why the equation in C6 has no solution at all.",
    ];

    private const string Title = "Linear Equations in One Variable";
    private const string Subtitle = "General Mathematics (GMATH801) - Class 8, Section A";

    private static List<DemoBlock> Blocks() =>
    [
        DemoBlock.Paragraph("The rule for this set is one step per line: whatever you do to one side, do to the other."),
        DemoBlock.Heading("What to do"),
        .. Tasks.Select(DemoBlock.Numbered),
        DemoBlock.Heading("How to hand it in"),
        DemoBlock.Bullet("Attach one file, and make sure every page is readable."),
    ];

    // ── The app's own gate ────────────────────────────────────────────────────

    /// <summary>
    /// The one check that matters most: each generated file passes the same validation an
    /// uploaded file does — extension allow-list, signature match, and a content type derived
    /// from the bytes rather than taken on trust. If a seeded attachment could not have been
    /// uploaded, it has no business being in the database.
    /// </summary>
    [Fact]
    public void EveryAttachmentKind_PassesTheUploadPolicyTheAppAppliesToRealUploads()
    {
        var policy = new FileUploadPolicy(Options.Create(new FileStorageOptions
        {
            Root = "./_test",
            MaxBytes = 2 * 1024 * 1024,
            AllowedExtensions = ["pdf", "txt", "png"],
        }));

        DemoDocument[] documents =
        [
            DemoDocuments.Pdf("worksheet", Title, Subtitle, Blocks()),
            DemoDocuments.PlainText("instructions", Title, Subtitle, Blocks()),
            DemoDocuments.Figure("figure", variant: 1),
        ];

        foreach (var document in documents)
        {
            using var content = new MemoryStream(document.Content);
            var result = policy.Validate(document.FileName, document.Content.Length, content);

            result.IsSuccess.Should().BeTrue(
                "{0} must survive the same validation an upload does, but was rejected: {1}",
                document.FileName,
                result.Error?.Message);

            // The seeder writes the policy's answer into the row, so the two must agree.
            result.Value!.ContentType.Should().Be(document.ContentType);
            result.Value.Extension.Should().Be(Path.GetExtension(document.FileName));
        }
    }

    // ── PDF ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Pdf_CrossReferenceTable_PointsAtTheStartOfEveryObject()
    {
        var bytes = DemoDocuments.Pdf("worksheet", Title, Subtitle, Blocks()).Content;
        var text = Latin1(bytes);

        var startXref = int.Parse(
            Regex.Match(text, @"startxref\s+(\d+)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        startXref.Should().BeLessThan(bytes.Length, "the trailer must point inside the file");
        text[startXref..].Should().StartWith("xref", "startxref locates the cross-reference table");

        var entries = Regex.Matches(text[startXref..], @"^(\d{10}) 00000 n $", RegexOptions.Multiline);
        var declared = int.Parse(
            Regex.Match(text, @"/Size (\d+)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        // /Size counts the free object 0 as well, which has its own entry rather than a match here.
        entries.Count.Should().Be(declared - 1);

        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            text[offset..].Should().StartWith(
                $"{i + 1} 0 obj",
                "the entry for object {0} must land exactly on its header",
                i + 1);
        }

        text.Should().StartWith("%PDF-1.4");
        text.TrimEnd().Should().EndWith("%%EOF");
    }

    [Fact]
    public void Pdf_DeclaresOnePageObjectPerPageAndAContentStreamForEach()
    {
        var bytes = DemoDocuments.Pdf("worksheet", Title, Subtitle, Blocks()).Content;
        var text = Latin1(bytes);

        var count = int.Parse(
            Regex.Match(text, @"/Count (\d+)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        count.Should().Be(1, "this much content fits on one A4 page");
        Regex.Matches(text, @"/Type /Page[^s]").Count.Should().Be(count);
        Regex.Matches(text, @"/Kids \[([^\]]+)\]").Single().Groups[1].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(count * 3);
    }

    [Fact]
    public void Pdf_ContentLongerThanAPage_BreaksOntoAnother()
    {
        var many = Enumerable.Range(0, 60)
            .Select(i => DemoBlock.Numbered($"Question {i}. " + Tasks[i % Tasks.Length]))
            .ToList();

        var text = Latin1(DemoDocuments.Pdf("worksheet", Title, Subtitle, many).Content);

        var count = int.Parse(
            Regex.Match(text, @"/Count (\d+)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        count.Should().BeGreaterThan(1);
        Regex.Matches(text, @"/Type /Page[^s]").Count.Should().Be(count);
        Regex.Matches(text, @"/Contents \d+ 0 R").Count.Should().Be(count);
    }

    [Fact]
    public void Pdf_DrawsTheTitleAndEveryTask()
    {
        var drawn = DrawnText(DemoDocuments.Pdf("worksheet", Title, Subtitle, Blocks()).Content);

        drawn.Should().Contain(Title);
        drawn.Should().Contain(Subtitle);

        foreach (var task in Tasks)
        {
            // Wrapping splits a task across several draw operations at its spaces, so rejoining
            // the operations with single spaces reconstructs it exactly.
            drawn.Should().Contain(task);
        }
    }

    /// <summary>
    /// Helvetica cannot draw a character WinAnsi has no code for, so the writer maps the ones
    /// this content uses and escapes them octally. An em dash left as UTF-8 would corrupt the
    /// stream length and render as two stray glyphs.
    /// </summary>
    [Fact]
    public void Pdf_EncodesNonAsciiAsWinAnsiOctalEscapes()
    {
        var bytes = DemoDocuments.Pdf("worksheet", "Sound — Frequency", "Physics • Class 10", []).Content;
        var text = Latin1(bytes);

        text.Should().Contain(@"Sound \227 Frequency", "an em dash is WinAnsi 0x97");
        text.Should().Contain(@"Physics \225 Class 10", "a bullet is WinAnsi 0x95");

        // Everything past the binary comment on line 2 is pure ASCII, which is what the octal
        // escaping buys: a stray UTF-8 sequence would both mis-render and break the stream length.
        bytes.Skip("%PDF-1.4\n".Length + 6).Should().OnlyContain(b => b <= 0x7E);
    }

    [Fact]
    public void Pdf_EscapesTheCharactersItsStringSyntaxReserves()
    {
        var drawn = DrawnText(DemoDocuments
            .Pdf("worksheet", "Brackets (round) and a backslash \\", string.Empty, [])
            .Content);

        drawn.Should().Contain(@"Brackets (round) and a backslash \");
    }

    /// <summary>Every literal string the content streams draw, rejoined in order.</summary>
    private static string DrawnText(byte[] pdf)
    {
        var literals = Regex.Matches(Latin1(pdf), @"\(((?:\\.|[^()\\])*)\) Tj")
            .Select(m => Regex.Replace(m.Groups[1].Value, @"\\(.)", "$1"));

        return string.Join(' ', literals);
    }

    // ── Plain text ────────────────────────────────────────────────────────────

    [Fact]
    public void PlainText_IsUtf8WithoutABomAndCarriesEveryTask()
    {
        var document = DemoDocuments.PlainText("instructions", Title, Subtitle, Blocks());

        document.Content.Take(3).Should().NotEqual([0xEF, 0xBB, 0xBF], "a BOM is not wanted here");
        document.Content.Take(8).Should().NotContain((byte)0x00, "the upload policy rejects text that looks binary");

        var text = Encoding.UTF8.GetString(document.Content);
        text.Should().Contain(Title.ToUpperInvariant());
        text.Should().Contain(Subtitle);

        foreach (var task in Tasks)
        {
            // Hanging-indent wrapping breaks lines and indents the continuations, so compare
            // against the text with its whitespace collapsed.
            Collapse(text).Should().Contain(task);
        }

        text.Should().Contain("1. ", "numbered items keep their numbering");
    }

    private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ");

    // ── PNG ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Png_HasAValidSignatureIntactChunkCrcsAndAFullyInflatableImage()
    {
        var bytes = DemoDocuments.Figure("figure", variant: 0).Content;

        bytes.Take(8).Should().Equal([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var chunks = ReadChunks(bytes);
        chunks.Select(c => c.Type).Should().ContainInOrder("IHDR", "IDAT", "IEND");

        var header = chunks.First(c => c.Type == "IHDR").Data;
        header.Should().HaveCount(13);
        var width = ReadBigEndian(header, 0);
        var height = ReadBigEndian(header, 4);
        width.Should().BeGreaterThan(0);
        height.Should().BeGreaterThan(0);
        header[8].Should().Be(8, "8 bits per channel");
        header[9].Should().Be(2, "colour type 2 is truecolour RGB");
        header[12].Should().Be(0, "not interlaced");

        // The payload must be a zlib stream (not raw deflate) that expands to exactly one filter
        // byte plus one RGB triple per pixel for every row — anything else and a decoder stops
        // partway through with a truncated image.
        var idat = chunks.Where(c => c.Type == "IDAT").SelectMany(c => c.Data).ToArray();
        using var compressed = new MemoryStream(idat);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        raw.Length.Should().Be(height * (1 + (width * 3)));

        // Filter 0 on every scanline is what the writer claims; a wrong byte here would make a
        // decoder apply a predictor that was never used.
        var scanlines = raw.ToArray();
        for (var y = 0; y < height; y++)
        {
            scanlines[y * (1 + (width * 3))].Should().Be(0);
        }
    }

    [Fact]
    public void Png_EachVariant_PlotsSomethingDifferent()
    {
        var rendered = Enumerable.Range(0, 4)
            .Select(v => Convert.ToBase64String(DemoDocuments.Figure("figure", v).Content))
            .ToList();

        rendered.Should().OnlyHaveUniqueItems("each variant plots its own curve in its own colour");
    }

    private static List<(string Type, byte[] Data)> ReadChunks(byte[] png)
    {
        var chunks = new List<(string, byte[])>();
        var crc = BuildCrcTable();
        var position = 8;

        while (position < png.Length)
        {
            var length = (int)ReadBigEndian(png, position);
            var type = Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png[(position + 8)..(position + 8 + length)];

            var expected = ReadBigEndian(png, position + 8 + length);
            var actual = Crc32(crc, png[(position + 4)..(position + 8 + length)]);
            actual.Should().Be(expected, "the CRC of the {0} chunk must match", type);

            chunks.Add((type, data));
            position += 12 + length;
        }

        return chunks;
    }

    private static uint ReadBigEndian(byte[] source, int offset) =>
        ((uint)source[offset] << 24) | ((uint)source[offset + 1] << 16)
        | ((uint)source[offset + 2] << 8) | source[offset + 3];

    /// <summary>
    /// The CRC-32 of the PNG specification, written independently of the encoder's copy — a
    /// shared implementation could agree with itself while disagreeing with every decoder.
    /// </summary>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(uint[] table, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    // ── Shared ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the bytes as Latin-1 so every byte maps to exactly one character. UTF-8 decoding
    /// would collapse the binary parts and move every offset the assertions check.
    /// </summary>
    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
