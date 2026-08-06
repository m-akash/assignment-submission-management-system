using System.Net;
using System.Text.RegularExpressions;

namespace AssignmentSystem.Infrastructure.Notifications;

/// <summary>
/// Derives a readable plain-text body from a notification's stored HTML, so the SMTP sender can
/// ship a <c>multipart/alternative</c> (HTML + text) without the notification row having to
/// persist a second column. Plaintext is never asserted on by the test suite; it exists purely
/// for mail clients and previews that prefer or require a text part.
///
/// Deliberately small and dependency-free. Pulling in an HTML parser (HtmlAgilityPack,
/// AngleSharp) for this would add a package for a job a few regexes do well enough: the goal is
/// readable text, not a faithful render. The conversions that matter for readability — block
/// closers to newlines, a label/value table row to <c>Label: Value</c>, entity decoding,
/// blank-line collapsing — are all below.
/// </summary>
internal static partial class HtmlToText
{
    public static string Convert(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = html!;

        // Drop the hidden preheader line entirely — it is inbox preview chrome, not content.
        text = PreheaderBlock().Replace(text, string.Empty);

        // A detail-table row renders as "Label: Value" on its own line.
        text = TableRowEnd().Replace(text, "\n");
        text = TableLabelToValue().Replace(text, ": ");

        // Block-level closers become line breaks so paragraphs and table rows separate.
        text = BlockClose().Replace(text, "\n");
        // <br> becomes a single newline.
        text = BrTag().Replace(text, "\n");

        // Strip everything that remains tag-shaped.
        text = AnyTag().Replace(text, string.Empty);

        // Undo the escaping applied for HTML, so "&amp;" reads as "&" again.
        text = WebUtility.HtmlDecode(text);

        // Normalise whitespace: trim each line, collapse runs of blanks.
        text = LineWhitespace().Replace(text, string.Empty);
        text = BlankRun().Replace(text, "\n\n");
        return text.Trim('\n', '\r', ' ', '\t');
    }

    // ── Regexes (source-generated for allocation-free, compiled-once matching) ──────────
    [GeneratedRegex(@"<div[^>]*style=""display:none[^>]*>.*?</div>", RegexOptions.Singleline)]
    private static partial Regex PreheaderBlock();

    [GeneratedRegex(@"</td>\s*</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex TableRowEnd();

    [GeneratedRegex(@"</th>\s*<td", RegexOptions.IgnoreCase)]
    private static partial Regex TableLabelToValue();

    [GeneratedRegex(@"</(p|div|tr|li|h[1-6]|table)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockClose();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTag();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex LineWhitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRun();
}
