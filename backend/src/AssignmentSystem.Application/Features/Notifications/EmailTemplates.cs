using System.Globalization;
using System.Text;

namespace AssignmentSystem.Application.Features.Notifications;

/// <summary>
/// The palette a fragment is drawn in. One small enum instead of a colour string per call
/// site, so "late submission" and "expiring link" always land on the same amber rather than
/// each composing its own approximation of it.
/// </summary>
internal enum Tone
{
    Brand,
    Info,
    Success,
    Warning,
    Danger,
    Neutral,
}

/// <summary>
/// The shared HTML layout for notification emails: one outer shell plus a set of reusable
/// fragments (a CTA button, a label/value detail table, status pills, a score bar, a file
/// list, callout boxes…). Inline CSS only and table-based structure, because that is what real
/// mail clients actually honour — a <c>&lt;style&gt;</c> block is stripped by enough of them
/// that relying on it would silently un-style the mail for a slice of recipients. Percentage
/// widths are avoided for the same reason (Outlook's Word rendering engine does not sum them
/// reliably); bars and progress fills use fixed pixel widths computed server-side instead.
///
/// This class trusts its inputs. It builds structure from already-safe HTML; the caller
/// (<see cref="NotificationMessages"/>) is the boundary that knows which entity fields are
/// user-authored and runs them through <c>WebUtility.HtmlEncode</c>. Escaping here would
/// either double-escape caller-escaped values or escape the tags this class itself emits.
/// The exception is text this class receives as plain, unescaped words (a badge label, an
/// eyebrow) — those are escaped internally so call sites never have to think about it.
///
/// The indigo accent (<c>#5b3df5</c>) mirrors the frontend <c>--brand</c> colour
/// (<c>oklch(0.52 0.21 278)</c>): mail and app read as one product.
/// </summary>
internal static class EmailTemplates
{
    // ── Brand ───────────────────────────────────────────────────────────────────
    private const string Brand = "#5b3df5";
    private const string BrandDark = "#4128d4";
    private const string BrandDarkest = "#2f1da8";
    private const string Ink = "#1e1e2a";
    private const string Muted = "#6b6b80";
    private const string Border = "#e6e6f0";
    private const string PanelBg = "#f6f5fb";

    // ── Tone palette ────────────────────────────────────────────────────────────
    // (background, foreground/border-accent, border) — pale fill + saturated text, so a pill
    // reads clearly without shouting, and a matching saturated colour for solid fills (bars,
    // buttons) that need real contrast rather than a tint.
    private static (string Bg, string Fg, string Border) ToneColors(Tone tone) => tone switch
    {
        Tone.Brand => ("#efeafe", BrandDark, "#d9d0fb"),
        Tone.Info => ("#eaf2ff", "#1d4ed8", "#bfdbfe"),
        Tone.Success => ("#eafbf1", "#067647", "#b7ebd2"),
        Tone.Warning => ("#fff7ea", "#92400e", "#fde3a7"),
        Tone.Danger => ("#fdecec", "#b42318", "#f8b4b4"),
        _ => ("#f3f3f8", "#55556b", "#e2e2ee"),
    };

    private static string ToneSolid(Tone tone) => tone switch
    {
        Tone.Brand => Brand,
        Tone.Info => "#2563eb",
        Tone.Success => "#16a34a",
        Tone.Warning => "#d97706",
        Tone.Danger => "#dc2626",
        _ => "#8a8aa0",
    };

    private static string ToneIcon(Tone tone) => tone switch
    {
        Tone.Success => "✅",
        Tone.Warning => "⚠️",
        Tone.Danger => "⛔",
        Tone.Info => "ℹ️",
        _ => "•",
    };

    /// <summary>
    /// The outer document: a centred, fixed-width card on a pale page, with a gradient header
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
            .Append("<meta name=\"color-scheme\" content=\"light\">")
            .Append("<meta name=\"supported-color-schemes\" content=\"light\">")
            .Append("<title>Assignment Management System</title></head>")
            .Append("<body style=\"margin:0;padding:0;background-color:#eceefb;\">")
            // Hidden preheader: a zero-font-size line that feeds the inbox preview text.
            .Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;\">")
            .Append(preheader).Append("</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"background-color:#eceefb;padding:36px 12px;\">")
            .Append("<tr><td align=\"center\">")
            .Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"width:600px;max-width:600px;background-color:#ffffff;border-radius:16px;")
            .Append("overflow:hidden;border:1px solid ").Append(Border)
            .Append(";box-shadow:0 4px 24px rgba(40,30,120,0.08);\">")
            // Header band: three-stop gradient + a small brand mark beside the wordmark.
            .Append("<tr><td style=\"padding:26px 32px;background:linear-gradient(135deg,")
            .Append(Brand).Append(' ').Append("0%,").Append(BrandDark).Append(' ').Append("55%,")
            .Append(BrandDarkest).Append(' ').Append("100%);\">")
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>")
            .Append("<td style=\"padding-right:10px;font-size:20px;line-height:1;\">📐</td>")
            .Append("<td style=\"font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:700;")
            .Append("color:#ffffff;letter-spacing:.3px;\">Assignment &amp; Submission Management System</td>")
            .Append("</tr></table>")
            .Append("</td></tr>")
            // Content.
            .Append("<tr><td style=\"padding:32px;\">").Append(contentHtml).Append("</td></tr>")
            // Footer.
            .Append("<tr><td style=\"padding:20px 32px;background-color:").Append(PanelBg)
            .Append(";border-top:1px solid ").Append(Border).Append(";\">")
            .Append("<p style=\"margin:0 0 4px;font-family:Arial,Helvetica,sans-serif;font-size:12px;")
            .Append("color:").Append(Muted).Append(";line-height:1.5;\">")
            .Append("This is an automated message from the Assignment &amp; Submission Management ")
            .Append("System. Please do not reply to this email.")
            .Append("</p>")
            .Append("<p style=\"margin:0;font-family:Arial,Helvetica,sans-serif;font-size:11px;")
            .Append("color:").Append(Muted).Append(";line-height:1.5;\">")
            .Append("If something here looks wrong, contact your school administrator rather than ")
            .Append("acting on this email alone.")
            .Append("</p></td></tr>")
            .Append("</table></td></tr></table>")
            .Append("</body></html>")
            .ToString();
    }

    /// <summary>
    /// A small uppercase pill above the main heading, naming what kind of mail this is at a
    /// glance ("NEW ASSIGNMENT", "GRADED"…) — the same job a section label does in the app.
    /// </summary>
    public static string Eyebrow(string text, Tone tone = Tone.Brand)
    {
        var (bg, fg, border) = ToneColors(tone);
        return new StringBuilder()
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 0 12px;\"><tr>")
            .Append("<td style=\"background-color:").Append(bg).Append(";color:").Append(fg)
            .Append(";border:1px solid ").Append(border).Append(";border-radius:999px;padding:5px 12px;")
            .Append("font-family:Arial,Helvetica,sans-serif;font-size:11px;font-weight:700;letter-spacing:.5px;\">")
            .Append(WebEscape(text.ToUpperInvariant()))
            .Append("</td></tr></table>")
            .ToString();
    }

    /// <summary>
    /// A small inline status pill, for a value cell in a <see cref="DetailTable"/> or inline
    /// in a sentence (submission status, late/on-time, active/inactive…).
    /// </summary>
    public static string Badge(string text, Tone tone = Tone.Neutral)
    {
        var (bg, fg, border) = ToneColors(tone);
        return new StringBuilder()
            .Append("<span style=\"display:inline-block;background-color:").Append(bg)
            .Append(";color:").Append(fg).Append(";border:1px solid ").Append(border)
            .Append(";border-radius:999px;padding:3px 10px;font-family:Arial,Helvetica,sans-serif;")
            .Append("font-size:12px;font-weight:700;white-space:nowrap;\">")
            .Append(WebEscape(text))
            .Append("</span>")
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
            .Append("style=\"margin:10px 0 4px;\"><tr><td align=\"center\" ")
            .Append("style=\"border-radius:9px;background:linear-gradient(135deg,")
            .Append(Brand).Append(',').Append(BrandDark).Append(");box-shadow:0 2px 8px rgba(91,61,245,0.35);\">")
            .Append("<a href=\"").Append(href).Append("\" ")
            .Append("style=\"display:inline-block;padding:13px 30px;font-family:Arial,Helvetica,sans-serif;")
            .Append("font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:9px;\">")
            .Append(label).Append("</a></td></tr></table>")
            .Append("<p style=\"margin:8px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:12px;")
            .Append("color:").Append(Muted).Append(";word-break:break-all;\">").Append(href).Append("</p>")
            .ToString();
    }

    /// <summary>
    /// A two-column label/value table for the structured facts of a notification (course,
    /// deadline, marks, IDs…). Values are assumed already escaped by the caller, or to be
    /// trusted HTML from another fragment in this class (a <see cref="Badge"/>, for instance).
    /// </summary>
    public static string DetailTable(params (string Label, string Value)[] rows)
    {
        if (rows is null || rows.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder()
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" ")
            .Append("style=\"width:100%;margin:10px 0;border-collapse:collapse;font-family:Arial,Helvetica,sans-serif;\">");

        foreach (var (label, value) in rows)
        {
            sb
                .Append("<tr>")
                .Append("<td style=\"padding:10px 14px;background-color:").Append(PanelBg)
                .Append(";border:1px solid ").Append(Border).Append(";font-size:13px;color:")
                .Append(Muted).Append(";white-space:nowrap;width:38%;\">")
                .Append(WebEscape(label)).Append("</td>")
                .Append("<td style=\"padding:10px 14px;border:1px solid ").Append(Border)
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
        $"<h2 style=\"margin:18px 0 8px;font-family:Arial,Helvetica,sans-serif;font-size:17px;" +
        $"font-weight:700;color:{Ink};\">{WebEscape(text)}</h2>";

    /// <summary>
    /// A large lede heading for the thing the mail is actually about (an assignment title, a
    /// class name…) — set above the greeting so the recipient knows what this is about before
    /// reading a single sentence of it. <paramref name="text"/> is trusted (already escaped by
    /// the caller), matching <see cref="Paragraph"/> — this carries entity data, not a static label.
    /// </summary>
    public static string Title(string text) =>
        $"<h1 style=\"margin:0 0 14px;font-family:Arial,Helvetica,sans-serif;font-size:21px;" +
        $"font-weight:700;color:{Ink};line-height:1.35;\">{text}</h1>";

    /// <summary>
    /// A quoted callout for verbatim text the recipient did not write themselves in this mail
    /// — teacher feedback, a preview of submitted content — visually set apart from the
    /// system's own sentences with a left accent bar.
    /// </summary>
    public static string Quote(string html) =>
        new StringBuilder()
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" ")
            .Append("style=\"width:100%;margin:6px 0 14px;\"><tr>")
            .Append("<td style=\"padding:14px 16px;background-color:").Append(PanelBg)
            .Append(";border-left:3px solid ").Append(Brand).Append(";border-radius:0 8px 8px 0;")
            .Append("font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.6;color:")
            .Append(Ink).Append(";font-style:italic;\">").Append(html).Append("</td></tr></table>")
            .ToString();

    /// <summary>
    /// A tone-coloured callout box for something that needs to stand out from the surrounding
    /// paragraphs — an expiring link, a late submission, a missing configuration. Stronger than
    /// <see cref="Note"/>, which is for routine asides.
    /// </summary>
    public static string InfoBox(string html, Tone tone = Tone.Info)
    {
        var (bg, fg, border) = ToneColors(tone);
        return new StringBuilder()
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" ")
            .Append("style=\"width:100%;margin:6px 0 14px;\"><tr>")
            .Append("<td style=\"padding:13px 15px;background-color:").Append(bg)
            .Append(";border:1px solid ").Append(border).Append(";border-radius:9px;")
            .Append("font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:1.6;color:")
            .Append(fg).Append(";\">").Append(ToneIcon(tone)).Append("&nbsp; ").Append(html)
            .Append("</td></tr></table>")
            .ToString();
    }

    /// <summary>A thin horizontal rule to separate distinct sections within the content area.</summary>
    public static string Divider() =>
        $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td " +
        $"style=\"padding:4px 0 16px;\"><div style=\"border-top:1px solid {Border};line-height:0;" +
        $"font-size:0;\">&nbsp;</div></td></tr></table>";

    /// <summary>
    /// A horizontal score bar plus its percentage, colour-banded (green/blue/amber/red) by how
    /// well the score sits against the maximum. Pixel-width fills rather than percentage
    /// widths, because Outlook's rendering engine does not sum percentage <c>&lt;td&gt;</c>
    /// widths reliably.
    /// </summary>
    public static string ScoreBar(decimal marks, decimal outOf)
    {
        var pct = PercentOf(marks, outOf);
        var tone = ToneForScore(pct);
        var fill = ToneSolid(tone);

        const int trackWidth = 220;
        var filledPx = (int)Math.Round(trackWidth * pct / 100.0, MidpointRounding.AwayFromZero);
        var emptyPx = trackWidth - filledPx;

        var sb = new StringBuilder()
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:6px 0 4px;\"><tr>")
            .Append("<td style=\"padding:0 10px 0 0;\">")
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"").Append(trackWidth)
            .Append("\" style=\"width:").Append(trackWidth)
            .Append("px;background-color:#eceefb;border-radius:6px;\"><tr>");

        if (filledPx > 0)
        {
            sb.Append("<td width=\"").Append(filledPx).Append("\" style=\"width:").Append(filledPx)
              .Append("px;height:10px;line-height:10px;font-size:0;background-color:").Append(fill)
              .Append(";border-radius:6px 0 0 6px;\">&nbsp;</td>");
        }

        if (emptyPx > 0)
        {
            sb.Append("<td width=\"").Append(emptyPx).Append("\" style=\"width:").Append(emptyPx)
              .Append("px;height:10px;line-height:10px;font-size:0;\">&nbsp;</td>");
        }

        sb.Append("</tr></table></td>")
          .Append("<td style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;font-weight:700;color:")
          .Append(fill).Append(";white-space:nowrap;\">")
          .Append(pct.ToString("0.#", CultureInfo.InvariantCulture)).Append("%</td>")
          .Append("</tr></table>");

        return sb.ToString();
    }

    /// <summary>A tone-coloured pill naming how a score reads ("Excellent · 92%"), for a
    /// <see cref="DetailTable"/> value cell alongside — or instead of — <see cref="ScoreBar"/>.</summary>
    public static string GradeBadge(decimal marks, decimal outOf)
    {
        var pct = PercentOf(marks, outOf);
        return Badge($"{GradeWord(pct)} · {pct.ToString("0.#", CultureInfo.InvariantCulture)}%", ToneForScore(pct));
    }

    /// <summary>
    /// A list of attachments as bordered rows, each with a paperclip glyph, the file name, and
    /// a human file size. Deliberately not linked — downloading requires an authenticated app
    /// session, so a dead link in an inbox would be worse than none.
    /// </summary>
    public static string FileList(IEnumerable<(string Name, long SizeBytes)> files)
    {
        var list = files as ICollection<(string Name, long SizeBytes)> ?? files.ToList();
        if (list.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var (name, sizeBytes) in list)
        {
            sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" ")
              .Append("style=\"width:100%;margin:0 0 6px;border-collapse:collapse;\"><tr>")
              .Append("<td style=\"padding:10px 12px;background-color:").Append(PanelBg)
              .Append(";border:1px solid ").Append(Border).Append(";border-radius:8px;")
              .Append("font-family:Arial,Helvetica,sans-serif;font-size:13px;color:").Append(Ink).Append(";\">")
              .Append("📎&nbsp; ").Append(WebEscape(name))
              .Append("&nbsp; <span style=\"color:").Append(Muted).Append(";font-size:12px;\">(")
              .Append(HumanFileSize(sizeBytes)).Append(")</span>")
              .Append("</td></tr></table>");
        }

        return sb.ToString();
    }

    /// <summary>Formats a byte count the way a person reads a file size, not the way a computer stores one.</summary>
    public static string HumanFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kb = bytes / 1024.0;
        if (kb < 1024)
        {
            return $"{kb.ToString("0.#", CultureInfo.InvariantCulture)} KB";
        }

        var mb = kb / 1024.0;
        return $"{mb.ToString("0.#", CultureInfo.InvariantCulture)} MB";
    }

    private static double PercentOf(decimal marks, decimal outOf) =>
        outOf > 0 ? Math.Clamp((double)(marks / outOf) * 100.0, 0, 100) : 0;

    private static Tone ToneForScore(double pct) => pct switch
    {
        >= 80 => Tone.Success,
        >= 60 => Tone.Info,
        >= 40 => Tone.Warning,
        _ => Tone.Danger,
    };

    private static string GradeWord(double pct) => pct switch
    {
        >= 90 => "Outstanding",
        >= 80 => "Excellent",
        >= 70 => "Good",
        >= 60 => "Satisfactory",
        >= 40 => "Needs improvement",
        _ => "Unsatisfactory",
    };

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
