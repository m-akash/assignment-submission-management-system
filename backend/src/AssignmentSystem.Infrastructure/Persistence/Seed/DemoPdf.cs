using System.Text;

namespace AssignmentSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// A minimal PDF writer, enough for the one document shape the seeder needs: A4 pages of
/// left-aligned text in Helvetica, headings in bold, paragraphs and lists, paginated when the
/// content outruns a page.
///
/// It writes the format directly — header, indirect objects, a cross-reference table with real
/// byte offsets, and a trailer — because that is a couple of hundred lines and a PDF toolkit is
/// a dependency the whole application would then carry for the sake of demo data. What comes out
/// is a genuine PDF: it opens in the browser's built-in viewer through the app's own preview,
/// downloads intact, and satisfies the <c>%PDF</c> signature check the upload policy applies.
///
/// Text is encoded as WinAnsi, which is what the two base fonts are declared with. Characters
/// outside it cannot be drawn by Helvetica at all, so the few that appear in this content —
/// dashes, curly quotes, the bullet — are mapped to their WinAnsi bytes, and anything else falls
/// back to a question mark rather than silently corrupting the stream.
///
/// Every number written into the file is formatted invariantly. A PDF is parsed by a machine
/// that has never heard of the seeding machine's locale, and a decimal comma in a coordinate
/// would produce a file that fails to open on exactly the developer machines set up that way.
/// </summary>
internal static class DemoPdf
{
    // A4 at 72 dpi, with a margin wide enough to read comfortably.
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double MarginLeft = 56;
    private const double MarginRight = 56;
    private const double TopY = PageHeight - 56;
    private const double BottomY = 56;

    private const double TitleSize = 16;
    private const double SubtitleSize = 9.5;
    private const double HeadingSize = 11.5;
    private const double BodySize = 10.5;

    /// <summary>Helvetica averages about half its point size per character; 0.52 leaves a little slack.</summary>
    private const double AverageGlyphWidth = 0.52;

    private const double Usable = PageWidth - MarginLeft - MarginRight;

    /// <summary>Indents for a list item's marker and for the lines that continue it.</summary>
    private const double MarkerIndent = 16;
    private const double BodyIndent = 30;

    public static byte[] Render(string title, string subtitle, IReadOnlyList<DemoBlock> blocks) =>
        Assemble(Paginate(Layout(title, subtitle, blocks)));

    /// <summary>Formats an interpolated string invariantly — see the note on locales above.</summary>
    private static string Inv(FormattableString text) => FormattableString.Invariant(text);

    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>One drawn line: text at a size and indent, or a horizontal rule.</summary>
    private sealed record Line(string Text, bool Bold, double Size, double Indent, double SpaceBefore, bool IsRule = false)
    {
        public static Line Rule(double spaceBefore) => new(string.Empty, false, 0, 0, spaceBefore, IsRule: true);
    }

    /// <summary>Turns blocks into the flat sequence of lines a page is drawn from.</summary>
    private static List<Line> Layout(string title, string subtitle, IReadOnlyList<DemoBlock> blocks)
    {
        var lines = new List<Line>();

        foreach (var text in DemoDocuments.Wrap(title, CharsFor(TitleSize, Usable)))
        {
            lines.Add(new Line(text, Bold: true, TitleSize, 0, lines.Count == 0 ? 0 : 3));
        }

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            foreach (var text in DemoDocuments.Wrap(subtitle, CharsFor(SubtitleSize, Usable)))
            {
                lines.Add(new Line(text, Bold: false, SubtitleSize, 0, 5));
            }
        }

        lines.Add(Line.Rule(10));

        foreach (var (block, ordinal) in DemoDocuments.Number(blocks))
        {
            switch (block.Kind)
            {
                case DemoBlockKind.Heading:
                    AddWrapped(lines, block.Text, bold: true, HeadingSize, indent: 0, spaceBefore: 16);
                    break;

                case DemoBlockKind.Numbered:
                    AddMarked(lines, Inv($"{ordinal}."), block.Text);
                    break;

                case DemoBlockKind.Bullet:
                    AddMarked(lines, "•", block.Text);
                    break;

                default:
                    AddWrapped(lines, block.Text, bold: false, BodySize, indent: 0, spaceBefore: 10);
                    break;
            }
        }

        return lines;
    }

    private static void AddWrapped(List<Line> lines, string text, bool bold, double size, double indent, double spaceBefore)
    {
        var wrapped = DemoDocuments.Wrap(text, CharsFor(size, Usable - indent));
        for (var i = 0; i < wrapped.Count; i++)
        {
            lines.Add(new Line(wrapped[i], bold, size, indent, i == 0 ? spaceBefore : 0));
        }
    }

    /// <summary>
    /// A list item: the marker sits in the left indent and the text hangs off it, so a wrapped
    /// item still reads as one item. The first text line cancels the marker's advance so the two
    /// share a baseline.
    /// </summary>
    private static void AddMarked(List<Line> lines, string marker, string text)
    {
        var wrapped = DemoDocuments.Wrap(text, CharsFor(BodySize, Usable - BodyIndent));

        lines.Add(new Line(marker, Bold: false, BodySize, MarkerIndent, 7));
        for (var i = 0; i < wrapped.Count; i++)
        {
            lines.Add(new Line(wrapped[i], Bold: false, BodySize, BodyIndent, i == 0 ? -Advance(BodySize) : 0));
        }
    }

    private static int CharsFor(double size, double width) => (int)(width / (size * AverageGlyphWidth));

    private static double Advance(double size) => size * 1.38;

    // ── Pagination ────────────────────────────────────────────────────────────

    /// <summary>Draws the lines into one content stream per page, breaking when the page fills.</summary>
    private static List<string> Paginate(List<Line> lines)
    {
        var pages = new List<string>();
        var page = new StringBuilder();
        var y = TopY;

        foreach (var line in lines)
        {
            var advance = line.IsRule ? 6 : Advance(line.Size);

            if (y - line.SpaceBefore - advance < BottomY && page.Length > 0)
            {
                pages.Add(page.ToString());
                page.Clear();
                y = TopY;
            }
            else
            {
                y -= line.SpaceBefore;
            }

            if (line.IsRule)
            {
                page.AppendLine(Inv(
                    $"0.6 w 0.78 0.80 0.84 RG {MarginLeft:0.##} {y:0.##} m {PageWidth - MarginRight:0.##} {y:0.##} l S"));
            }
            else
            {
                var font = line.Bold ? "F2" : "F1";
                page.AppendLine(Inv(
                    $"BT /{font} {line.Size:0.##} Tf {MarginLeft + line.Indent:0.##} {y:0.##} Td ({Escape(line.Text)}) Tj ET"));
            }

            y -= advance;
        }

        if (page.Length > 0 || pages.Count == 0)
        {
            pages.Add(page.ToString());
        }

        return pages;
    }

    // ── File assembly ─────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the objects in ascending number order, recording where each one starts, then the
    /// cross-reference table those offsets belong to. Object numbers: 1 catalogue, 2 page tree,
    /// 3 and 4 the two fonts, then one content stream and one page node per page.
    /// </summary>
    private static byte[] Assemble(List<string> pages)
    {
        const int firstContent = 5;
        var firstPage = firstContent + pages.Count;
        var total = 4 + (2 * pages.Count);

        using var file = new MemoryStream();
        var offsets = new List<long>(total);

        void Write(string text) => file.Write(Encoding.ASCII.GetBytes(text));

        void WriteObject(string body)
        {
            offsets.Add(file.Position);
            Write(Inv($"{offsets.Count} 0 obj\n{body}\nendobj\n"));
        }

        Write("%PDF-1.4\n");

        // The conventional binary comment: it marks the file as binary for tools that sniff the
        // first two lines, keeping a transfer that "helpfully" rewrites line endings from
        // treating the file as text.
        file.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);

        WriteObject("<< /Type /Catalog /Pages 2 0 R >>");

        var kids = string.Join(' ', Enumerable.Range(firstPage, pages.Count).Select(n => Inv($"{n} 0 R")));
        WriteObject(Inv(
            $"<< /Type /Pages /Count {pages.Count} /Kids [{kids}] /MediaBox [0 0 {PageWidth:0.##} {PageHeight:0.##}] >>"));

        WriteObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        WriteObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        foreach (var content in pages)
        {
            WriteObject(Inv(
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream"));
        }

        for (var i = 0; i < pages.Count; i++)
        {
            WriteObject(Inv(
                $"<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {firstContent + i} 0 R >>"));
        }

        var startXref = file.Position;
        Write(Inv($"xref\n0 {total + 1}\n"));
        Write("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            // Every entry is exactly 20 bytes wide — the format gives no other way to find one.
            Write(Inv($"{offset:D10} 00000 n \n"));
        }

        Write(Inv($"trailer\n<< /Size {total + 1} /Root 1 0 R >>\nstartxref\n{startXref}\n%%EOF\n"));

        return file.ToArray();
    }

    // ── Text encoding ─────────────────────────────────────────────────────────

    /// <summary>
    /// The characters this content uses that sit outside Latin-1 but inside WinAnsi. Without
    /// this mapping an em dash would be dropped by a viewer rather than drawn.
    /// </summary>
    private static readonly Dictionary<char, byte> WinAnsiExtras = new()
    {
        ['•'] = 0x95, // bullet
        ['–'] = 0x96, // en dash
        ['—'] = 0x97, // em dash
        ['‘'] = 0x91,
        ['’'] = 0x92,
        ['“'] = 0x93,
        ['”'] = 0x94,
        ['…'] = 0x85, // ellipsis
        ['−'] = 0x2D, // minus sign, drawn as a hyphen
    };

    /// <summary>
    /// Renders a string as the body of a PDF literal string: WinAnsi bytes, with the three
    /// characters the syntax reserves escaped and everything unprintable written as an octal
    /// escape, so the content stream itself stays ASCII on disk.
    /// </summary>
    private static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length + 8);

        foreach (var character in text)
        {
            var code = ToWinAnsi(character);

            if (code is (byte)'(' or (byte)')' or (byte)'\\')
            {
                escaped.Append('\\').Append((char)code);
            }
            else if (code is < 32 or > 126)
            {
                escaped.Append('\\').Append(Convert.ToString((int)code, 8).PadLeft(3, '0'));
            }
            else
            {
                escaped.Append((char)code);
            }
        }

        return escaped.ToString();
    }

    private static byte ToWinAnsi(char character)
    {
        if (character < 128)
        {
            return (byte)character;
        }

        if (WinAnsiExtras.TryGetValue(character, out var mapped))
        {
            return mapped;
        }

        // Latin-1 and WinAnsi agree from 0xA0 up, which covers the accented letters and symbols
        // this content can contain. Anything else has no glyph in a base font at all.
        return character is >= (char)0xA0 and <= (char)0xFF ? (byte)character : (byte)'?';
    }
}
