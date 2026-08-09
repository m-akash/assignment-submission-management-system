using System.Net;
using Ganss.Xss;

namespace AssignmentSystem.Application.Common.Html;

/// <summary>
/// The write boundary for the one field in the system that stores markup: an assignment's
/// description, authored in the client's rich-text editor.
///
/// Storing HTML makes a teacher's brief a script-injection vector against their own class —
/// the description is written by one user and rendered into every enrolled student's browser
/// and inbox, which is precisely the shape of a stored XSS. Escaping it on output is not
/// available here, because the whole point is that the markup renders. So the defence is an
/// allowlist applied on the way in: <see cref="Sanitize"/> parses the document and keeps only
/// the tags and attributes the editor can actually produce, dropping everything else —
/// scripts, event handlers, styles, iframes, <c>javascript:</c> URLs, embedded data.
///
/// The allowlist is deliberately the editor's feature set and not a character more. Anything
/// a teacher can type survives the round trip untouched; anything they cannot type had to have
/// been hand-crafted against the API, and is exactly what should not survive it.
///
/// Both sanitizers are configured once and then only read, which is the usage the library
/// documents as thread-safe.
/// </summary>
public static class HtmlContent
{
    /// <summary>What the editor's toolbar can produce, and nothing else.</summary>
    private static readonly string[] AllowedTags =
    [
        "p", "br", "strong", "b", "em", "i", "u", "s", "code",
        "h2", "h3", "ul", "ol", "li", "blockquote", "a",
    ];

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();
    private static readonly HtmlSanitizer TextExtractor = CreateTextExtractor();

    /// <summary>
    /// Reduces authored markup to the allowlist. Call this on the way into storage, never on
    /// the way out: sanitizing on read would leave the unsafe original in the database for
    /// whichever consumer forgets to.
    /// </summary>
    public static string Sanitize(string? html) =>
        string.IsNullOrWhiteSpace(html) ? string.Empty : Sanitizer.Sanitize(html).Trim();

    /// <summary>
    /// <see cref="Sanitize"/> for fields where "no answer" is a real state rather than a
    /// validation failure. An editor that was typed into and cleared posts
    /// <c>&lt;p&gt;&lt;/p&gt;</c>, which is not null and not whitespace — so without this,
    /// rules like "a submission must include a text answer or a file" would count an empty
    /// editor as an answer and let a student submit nothing at all.
    /// </summary>
    public static string? SanitizeOrNull(string? html)
    {
        var sanitized = Sanitize(html);
        return HasText(sanitized) ? sanitized : null;
    }

    /// <summary>
    /// The words inside the markup, with the tags gone — for emptiness checks and anywhere
    /// formatting cannot be shown. Values written before the editor existed are plain text
    /// already and pass through unharmed.
    /// </summary>
    public static string ToPlainText(string? html) =>
        string.IsNullOrWhiteSpace(html)
            ? string.Empty
            // The extractor re-encodes what it emits, so entities are decoded back afterwards:
            // "&amp;" the author typed should count as one character, not five.
            : WebUtility.HtmlDecode(TextExtractor.Sanitize(html)).Trim();

    /// <summary>
    /// Whether a value carries any actual words. This is what "required" has to mean for a
    /// rich-text field: an editor that was typed into and then cleared still posts
    /// <c>&lt;p&gt;&lt;/p&gt;</c>, which is not empty by any string measure.
    /// </summary>
    public static bool HasText(string? html) => ToPlainText(html).Length > 0;

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        foreach (var tag in AllowedTags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        // Links only, and only the parts of a link that are not a rendering instruction.
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");
        sanitizer.AllowDataAttributes = false;

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        // No inline styling at all: a description is rendered inside the app's own panels and
        // the email shell, and author-supplied CSS is how a brief ends up covering the page
        // it sits on.
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowedClasses.Clear();

        return sanitizer;
    }

    /// <summary>
    /// The same parser aimed at the opposite result: no tag is allowed, but the text inside
    /// each one is kept as it is unwrapped, which leaves the document's words behind.
    /// </summary>
    private static HtmlSanitizer CreateTextExtractor()
    {
        var extractor = new HtmlSanitizer();

        extractor.AllowedTags.Clear();
        extractor.AllowedAttributes.Clear();
        extractor.KeepChildNodes = true;

        return extractor;
    }
}
