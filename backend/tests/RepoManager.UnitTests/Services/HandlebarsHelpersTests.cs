using FluentAssertions;
using HandlebarsDotNet;
using RepoManager.Infrastructure.Services.Handlebars;

namespace RepoManager.UnitTests.Services;

[Trait("Category", "Unit")]
public class HandlebarsHelpersTests
{
    private readonly IHandlebars _hbs;

    public HandlebarsHelpersTests()
    {
        var recorder = new MissingTokenRecorder();
        _hbs = HandlebarsFactory.Create(recorder);
    }

    private string Render(string template, object context) =>
        _hbs.Compile(template)(context);

    [Fact]
    public void FormatDate_ValidDateTimeOffset_FormatsWithGivenPattern()
    {
        var ctx = new { d = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero) };
        var result = Render("{{formatDate d \"yyyy-MM-dd\"}}", ctx);
        result.Should().Be("2025-03-15");
    }

    [Fact]
    public void FormatDate_NoFormat_UsesDefaultPattern()
    {
        var ctx = new { d = new DateTimeOffset(2025, 1, 5, 0, 0, 0, TimeSpan.Zero) };
        var result = Render("{{formatDate d}}", ctx);
        result.Should().Be("2025-01-05");
    }

    [Fact]
    public void Length_List_ReturnsCount()
    {
        var ctx = new { items = new List<string> { "a", "b", "c" } };
        var result = Render("{{length items}}", ctx);
        result.Should().Be("3");
    }

    [Fact]
    public void Length_EmptyList_ReturnsZero()
    {
        var ctx = new { items = new List<string>() };
        var result = Render("{{length items}}", ctx);
        result.Should().Be("0");
    }

    [Fact]
    public void Eq_EqualValues_ReturnsTrue()
    {
        var ctx = new { v = "1.0.0" };
        var result = Render("{{#if (eq v \"1.0.0\")}}yes{{else}}no{{/if}}", ctx);
        result.Should().Be("yes");
    }

    [Fact]
    public void Eq_DifferentValues_ReturnsFalse()
    {
        var ctx = new { v = "2.0.0" };
        var result = Render("{{#if (eq v \"1.0.0\")}}yes{{else}}no{{/if}}", ctx);
        result.Should().Be("no");
    }

    [Fact]
    public void Gt_FirstGreater_ReturnsTrue()
    {
        var ctx = new { a = 5, b = 3 };
        var result = Render("{{#if (gt a b)}}yes{{else}}no{{/if}}", ctx);
        result.Should().Be("yes");
    }

    [Fact]
    public void Gt_FirstNotGreater_ReturnsFalse()
    {
        var ctx = new { a = 2, b = 5 };
        var result = Render("{{#if (gt a b)}}yes{{else}}no{{/if}}", ctx);
        result.Should().Be("no");
    }

    [Fact]
    public void Minus_TwoNumbers_ReturnsDifference()
    {
        var ctx = new { a = 10, b = 3 };
        var result = Render("{{minus a b}}", ctx);
        result.Should().Be("7");
    }

    [Fact]
    public void Lower_UppercaseString_ReturnsLowercase()
    {
        var ctx = new { s = "HELLO" };
        var result = Render("{{lower s}}", ctx);
        result.Should().Be("hello");
    }

    [Fact]
    public void Upper_LowercaseString_ReturnsUppercase()
    {
        var ctx = new { s = "hello" };
        var result = Render("{{upper s}}", ctx);
        result.Should().Be("HELLO");
    }

    [Fact]
    public void Truncate_LongString_TruncatesWithEllipsis()
    {
        var ctx = new { s = "Hello World" };
        var result = Render("{{truncate s 5}}", ctx);
        result.Should().Be("Hello…");
    }

    [Fact]
    public void Truncate_ShortString_ReturnsUnchanged()
    {
        var ctx = new { s = "Hi" };
        var result = Render("{{truncate s 10}}", ctx);
        result.Should().Be("Hi");
    }

    [Fact]
    public void JiraLink_TicketId_ReturnsFormattedLink()
    {
        var ctx = new { ticket = "PAY-123" };
        var result = Render("{{jiraLink ticket}}", ctx);
        result.Should().Be("[PAY-123]");
    }
}
