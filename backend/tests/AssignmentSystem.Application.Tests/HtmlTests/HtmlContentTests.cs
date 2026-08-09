using AssignmentSystem.Application.Common.Html;
using AssignmentSystem.Application.Features.Notifications;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Application.Tests.HtmlTests;

/// <summary>
/// The allowlist that makes it safe to render an assignment description as markup.
/// A teacher writes the brief and every student in the class renders it, in their browser
/// and in their inbox — so these are not style rules, they are the reason a description
/// cannot be turned into a script that runs against a whole class.
/// </summary>
public class HtmlContentTests
{
    [Theory]
    [InlineData("<p>Read <strong>chapter 4</strong> and answer <em>all</em> questions.</p>")]
    [InlineData("<h2>Part one</h2><ul><li>Draw the graph</li><li>Explain it</li></ul>")]
    [InlineData("<ol><li>Set up</li></ol><blockquote>Show your working.</blockquote>")]
    [InlineData("<p>Use <code>O(n log n)</code> or better.</p>")]
    public void Sanitize_WithMarkupTheEditorProduces_ShouldLeaveItIntact(string html)
    {
        HtmlContent.Sanitize(html).Should().Be(html);
    }

    [Theory]
    [InlineData("<p>Hi</p><script>steal()</script>", "script")]
    [InlineData("<p onclick=\"steal()\">Hi</p>", "onclick")]
    [InlineData("<p style=\"position:fixed;inset:0\">Hi</p>", "style")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe><p>Hi</p>", "iframe")]
    [InlineData("<img src=\"x\" onerror=\"steal()\"><p>Hi</p>", "onerror")]
    [InlineData("<p>Hi</p><object data=\"x\"></object>", "object")]
    public void Sanitize_WithAnythingTheEditorCannotProduce_ShouldStripIt(string html, string forbidden)
    {
        var sanitized = HtmlContent.Sanitize(html);

        sanitized.Should().NotContain(forbidden);
        sanitized.Should().Contain("Hi", "stripping the payload should not take the brief with it");
    }

    [Fact]
    public void Sanitize_WithAScriptedHref_ShouldDropTheLinkButKeepTheWords()
    {
        var sanitized = HtmlContent.Sanitize("<p><a href=\"javascript:steal()\">Course notes</a></p>");

        sanitized.Should().NotContain("javascript");
        sanitized.Should().Contain("Course notes");
    }

    [Fact]
    public void Sanitize_WithAnOrdinaryLink_ShouldKeepTheHref()
    {
        var sanitized = HtmlContent.Sanitize(
            "<p><a href=\"https://example.com/notes\" target=\"_blank\" rel=\"noopener\">Notes</a></p>");

        sanitized.Should().Contain("https://example.com/notes");
        sanitized.Should().Contain("target=\"_blank\"");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("<p></p>", false)]
    [InlineData("<p><br></p>", false)]
    [InlineData("<p>   </p>", false)]
    [InlineData("<p>Answer both parts.</p>", true)]
    // Written before the editor existed, and still perfectly valid.
    [InlineData("Answer both parts.", true)]
    public void HasText_ShouldJudgeTheWordsRatherThanTheMarkup(string html, bool expected)
    {
        HtmlContent.HasText(html).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p></p>")]
    [InlineData("<p><br></p>")]
    // Everything here is stripped, so what is left is an answer made of nothing.
    [InlineData("<script>steal()</script>")]
    public void SanitizeOrNull_WithNothingWritten_ShouldReturnNull(string? html)
    {
        HtmlContent.SanitizeOrNull(html).Should().BeNull();
    }

    [Fact]
    public void SanitizeOrNull_WithAnAnswer_ShouldReturnTheSanitizedMarkup()
    {
        HtmlContent.SanitizeOrNull("<p>Because <em>x</em> is prime.</p><script>steal()</script>")
            .Should().Be("<p>Because <em>x</em> is prime.</p>");
    }

    [Fact]
    public void ToPlainText_ShouldReturnTheWordsWithEntitiesDecoded()
    {
        HtmlContent.ToPlainText("<p>Compare <strong>a &amp; b</strong></p>")
            .Should().Be("Compare a & b");
    }

    [Fact]
    public void EmailRichText_ShouldInlineStylesRatherThanEscapeTheMarkup()
    {
        var body = EmailTemplates.RichText("<h2>Part one</h2><ul><li>Draw the graph</li></ul>");

        // The tags are applied, not printed…
        body.Should().NotContain("&lt;");
        // …and each one carries its own styling, because the mail has no stylesheet to fall
        // back on.
        body.Should().Contain("<h2 style=\"");
        body.Should().Contain("<ul style=\"");
        body.Should().Contain("<li style=\"");
        body.Should().Contain("Draw the graph");
    }
}
