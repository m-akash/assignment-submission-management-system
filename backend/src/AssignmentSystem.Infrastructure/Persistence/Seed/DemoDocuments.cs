using System.Text;

namespace AssignmentSystem.Infrastructure.Persistence.Seed;

/// <summary>How one line of a demo document is meant to read.</summary>
internal enum DemoBlockKind
{
    /// <summary>A section title — bold in the PDF, underlined in the text file.</summary>
    Heading,
    Paragraph,

    /// <summary>An item in a numbered run. Numbering restarts after any other kind of block.</summary>
    Numbered,
    Bullet,
}

/// <summary>
/// A line of document content, written once and rendered into whichever format the
/// attachment happens to be. Keeping the content format-independent is what lets an
/// assignment's worksheet and its plain-text instruction sheet say the same thing.
/// </summary>
internal readonly record struct DemoBlock(DemoBlockKind Kind, string Text)
{
    public static DemoBlock Heading(string text) => new(DemoBlockKind.Heading, text);

    public static DemoBlock Paragraph(string text) => new(DemoBlockKind.Paragraph, text);

    public static DemoBlock Numbered(string text) => new(DemoBlockKind.Numbered, text);

    public static DemoBlock Bullet(string text) => new(DemoBlockKind.Bullet, text);
}

/// <summary>
/// An attachment with its bytes in hand, ready for <c>IFileStorage</c>. The content type is
/// the one <c>FileUploadPolicy</c> would have derived from the extension, so a seeded row is
/// indistinguishable from an uploaded one.
/// </summary>
internal sealed record DemoDocument(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Builds the attachments the seeder hangs off assignments and submissions.
///
/// These are real files, not placeholders: a PDF that opens in the browser's own viewer, a
/// UTF-8 text file, and a PNG that decodes as an image. That matters because "view and
/// download an attachment" is a feature of the app, and a zero-byte or fake-header file would
/// leave it undemonstrable on a fresh checkout — the upload policy checks file signatures, so
/// nothing that merely claims to be a PDF would survive the same path a real upload takes.
///
/// Everything is written by hand rather than through an imaging or PDF library. A seeder is not
/// worth a dependency, and the two libraries that would do it (System.Drawing, any PDF toolkit)
/// are respectively unsupported on the Linux container this runs in and far larger than the few
/// hundred bytes of format each file actually needs.
/// </summary>
internal static class DemoDocuments
{
    /// <summary>A single-column A4 document — the worksheet a teacher attaches, or a student's answer sheet.</summary>
    public static DemoDocument Pdf(string fileName, string title, string subtitle, IReadOnlyList<DemoBlock> blocks) =>
        new(EnsureExtension(fileName, ".pdf"), "application/pdf", DemoPdf.Render(title, subtitle, blocks));

    /// <summary>The same content as plain UTF-8 text, for the attachments that are instruction sheets.</summary>
    public static DemoDocument PlainText(string fileName, string title, string subtitle, IReadOnlyList<DemoBlock> blocks) =>
        new(EnsureExtension(fileName, ".txt"), "text/plain", RenderText(title, subtitle, blocks));

    /// <summary>
    /// A plotted figure — graph paper with a curve on it. <paramref name="variant"/> chooses
    /// which curve, so a physics brief and an integration brief do not attach the same picture.
    /// </summary>
    public static DemoDocument Figure(string fileName, int variant) =>
        new(EnsureExtension(fileName, ".png"), "image/png", DemoPng.RenderChart(variant));

    // ── Plain text ────────────────────────────────────────────────────────────

    private const int TextWidth = 78;

    private static byte[] RenderText(string title, string subtitle, IReadOnlyList<DemoBlock> blocks)
    {
        var text = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            text.Append(subtitle).Append('\n');
        }

        text.Append(title.ToUpperInvariant()).Append('\n');
        text.Append(new string('=', Math.Min(TextWidth, Math.Max(title.Length, TextWidth / 2)))).Append('\n');

        foreach (var (block, ordinal) in Number(blocks))
        {
            text.Append('\n');

            switch (block.Kind)
            {
                case DemoBlockKind.Heading:
                    text.Append(block.Text).Append('\n');
                    text.Append(new string('-', Math.Min(TextWidth, block.Text.Length))).Append('\n');
                    break;

                case DemoBlockKind.Numbered:
                    AppendHanging(text, $"  {ordinal}. ", block.Text);
                    break;

                case DemoBlockKind.Bullet:
                    AppendHanging(text, "  * ", block.Text);
                    break;

                default:
                    foreach (var line in Wrap(block.Text, TextWidth))
                    {
                        text.Append(line).Append('\n');
                    }

                    break;
            }
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
    }

    /// <summary>Writes a marked item with its continuation lines indented to clear the marker.</summary>
    private static void AppendHanging(StringBuilder text, string marker, string body)
    {
        var continuation = new string(' ', marker.Length);
        var lines = Wrap(body, TextWidth - marker.Length);

        for (var i = 0; i < lines.Count; i++)
        {
            text.Append(i == 0 ? marker : continuation).Append(lines[i]).Append('\n');
        }
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Greedy word wrap. A word longer than the line is left to overhang rather than being
    /// broken — nothing in this content is, and a hard break would be the worse failure.
    /// </summary>
    internal static List<string> Wrap(string text, int maxChars)
    {
        var lines = new List<string>();
        if (maxChars < 8)
        {
            maxChars = 8;
        }

        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > maxChars)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }

    /// <summary>
    /// Pairs each block with its position in the current numbered run — 0 for anything that is
    /// not numbered. Both renderers need the same numbering, so neither one counts for itself.
    /// </summary>
    internal static IEnumerable<(DemoBlock Block, int Ordinal)> Number(IReadOnlyList<DemoBlock> blocks)
    {
        var ordinal = 0;
        foreach (var block in blocks)
        {
            if (block.Kind == DemoBlockKind.Numbered)
            {
                yield return (block, ++ordinal);
            }
            else
            {
                ordinal = 0;
                yield return (block, 0);
            }
        }
    }

    private static string EnsureExtension(string fileName, string extension) =>
        fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? fileName : fileName + extension;
}
