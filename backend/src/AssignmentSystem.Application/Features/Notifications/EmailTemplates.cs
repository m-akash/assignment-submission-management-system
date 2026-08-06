using System.Globalization;
using System.Text;

namespace AssignmentSystem.Application.Features.Notifications;

/// <summary>
/// The shared HTML layout for notification emails: one outer shell plus a handful of reusable
/// fragments (a CTA button, a label/value detail table, a paragraph). Inline CSS only and
/// table-based structure, because that is what real mail clients actually honour — a
/// <c>&lt;style&gt;</c> block is stripped by enough of them that relying on it would silently
/// un-style the mail for a slice of recipients.
///
/// This class trusts its inputs. It builds structure from already-safe HTML; the caller
/// (<see cref="NotificationMessages"/>) is the boundary that knows which entity fields are
/// user-authored and runs them through <c>WebUtility.HtmlEncode</c>. Escaping here would
/// either double-escape caller-escaped values or escape the tags this class itself emits.
///
/// The indigo accent (<c>#5b3df5</c>) mirrors the frontend <c>--brand</c> colour
/// (<c>oklch(0.52 0.21 278)</c>): mail and app read as one product.
/// </summary>
internal static class EmailTemplates
{
    // ── Brand ───────────────────────────────────────────────────────────────────
    private const string Brand = "#5b3df5";
    private const string BrandDark = "#4128d4";
    private const string Ink = "#1e1e2a";
    private const string Muted = "#6b6b80";
    private const string Border = "#e6e6f0";
    private const string PanelBg = "#f6f5fb";

    /// <summary>
    /// The outer document: a centred, fixed-width card on a pale page, with an indigo header
    /// band carrying the wordmark, the supplied content, and a muted footer.
    ///
    /// <paramref name="preheader"/> is the hidden preview line mail clients show beside the
    /// subject; keep it short (it is clipped past ~85 chars by some clients).
    /// </summary>
    public static string Shell(string preheader, string contentHtml)
    {
        preheader = string.IsNullOrWhiteSpace(preheader) ? string.Empty : WebEscape(preheader);

        return new StringBuilder()
            .Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">")
            .Append("<title>Assignment Management System</title></head>")
            .Append("<body style=\"margin:0;padding:0;background-color:#eceefb;\">")
            // Hidden preheader: a zero-font-size line that feeds the inbox preview text.
            .Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;\">")
            .Append(preheader).Append("</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"background-color:#eceefb;padding:32px 12px;\">")
            .Append("<tr><td align=\"center\">")
            .Append("<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"width:560px;max-width:560px;background-color:#ffffff;border-radius:14px;")
            .Append("overflow:hidden;border:1px solid ").Append(Border)
            .Append(";box-shadow:0 2px 10px rgba(40,30,120,0.06);\">")
            // Header band.
            .Append("<tr><td style=\"padding:22px 32px;background:linear-gradient(135deg,")
            .Append(Brand).Append(',').Append(BrandDark).Append(");\">")
            .Append("<span style=\"font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:700;")
            .Append("color:#ffffff;letter-spacing:.3px;\">").Append(Wordmark()).Append("</span>")
            .Append("</td></tr>")
            // Content.
            .Append("<tr><td style=\"padding:32px;\">").Append(contentHtml).Append("</td></tr>")
            // Footer.
            .Append("<tr><td style=\"padding:18px 32px;background-color:").Append(PanelBg)
            .Append(";border-top:1px solid ").Append(Border).Append(";\">")
            .Append("<p style=\"margin:0;font-family:Arial,Helvetica,sans-serif;font-size:12px;")
            .Append("color:").Append(Muted).Append(";line-height:1.5;\">")
            .Append("This is an automated message from the Assignment &amp; Submission Management ")
            .Append("System. Please do not reply to this email.")
            .Append("</p></td></tr>")
            .Append("</table></td></tr></table>")
            .Append("</body></html>")
            .ToString();
    }

    /// <summary>
    /// A primary call-to-action button, centred. Renders the <paramref name="href"/> as a small
    /// visible URL beneath the button too: many mail clients disable link-wrapped buttons on a
    /// first send, and a recipient whose button does nothing should still be able to copy the
    /// link. The caller must pass an already-absolute URL — a relative path would be useless in
    /// an inbox.
    /// </summary>
    public static string Button(string href, string label)
    {
        href = WebEscape(href);
        label = WebEscape(label);

        return new StringBuilder()
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"margin:8px 0 4px;\"><tr><td align=\"center\" ")
            .Append("style=\"border-radius:8px;background-color:").Append(Brand).Append(";\">")
            .Append("<a href=\"").Append(href).Append("\" ")
            .Append("style=\"display:inline-block;padding:12px 28px;font-family:Arial,Helvetica,sans-serif;")
            .Append("font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;\">")
            .Append(label).Append("</a></td></tr></table>")
            .Append("<p style=\"margin:6px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:12px;")
            .Append("color:").Append(Muted).Append(";word-break:break-all;\">").Append(href).Append("</p>")
            .ToString();
    }

    /// <summary>
    /// A two-column label/value table for the structured facts of a notification (course,
    /// deadline, marks, IDs…). Values are assumed already escaped by the caller.
    /// </summary>
    public static string DetailTable(params (string Label, string Value)[] rows)
    {
        if (rows is null || rows.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder()
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" ")
            .Append("style=\"width:100%;margin:8px 0;border-collapse:collapse;font-family:Arial,Helvetica,sans-serif;\">");

        foreach (var (label, value) in rows)
        {
            sb
                .Append("<tr>")
                .Append("<td style=\"padding:9px 14px;background-color:").Append(PanelBg)
                .Append(";border:1px solid ").Append(Border).Append(";font-size:13px;color:")
                .Append(Muted).Append(";white-space:nowrap;width:38%;\">")
                .Append(WebEscape(label)).Append("</td>")
                .Append("<td style=\"padding:9px 14px;border:1px solid ").Append(Border)
                .Append(";font-size:14px;color:").Append(Ink).Append(";font-weight:500;\">")
                .Append(value).Append("</td>")
                .Append("</tr>");
        }

        return sb.Append("</table>").ToString();
    }

    /// <summary>A body paragraph. <paramref name="html"/> is trusted (already escaped).</summary>
    public static string Paragraph(string html) =>
        $"<p style=\"margin:0 0 12px;font-family:Arial,Helvetica,sans-serif;font-size:15px;" +
        $"line-height:1.6;color:{Ink};\">{html}</p>";

    /// <summary>A muted helper line (e.g. an "expires on…" note) under a paragraph.</summary>
    public static string Note(string html) =>
        $"<p style=\"margin:0 0 12px;font-family:Arial,Helvetica,sans-serif;font-size:13px;" +
        $"line-height:1.5;color:{Muted};\">{html}</p>";

    /// <summary>A section heading above a block of content.</summary>
    public static string Heading(string text) =>
        $"<h2 style=\"margin:16px 0 8px;font-family:Arial,Helvetica,sans-serif;font-size:17px;" +
        $"font-weight:700;color:{Ink};\">{WebEscape(text)}</h2>";

    private static string Wordmark() =>
        "📐 Assignment Management System";

    /// <summary>
    /// Escapes for placement in HTML text/attribute content. A thin wrapper so call sites read
    /// as intent rather than as a BCL call, and so this class is internally consistent (it
    /// escapes its own static text via this too).
    /// </summary>
    private static string WebEscape(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Formats a UTC instant the way the rest of the system does, for the rare spot inside the
    /// template layer that needs a date (kept here so date wording stays in one place).
    /// </summary>
    public static string FormatUtc(DateTime utc) =>
        utc.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
