using Bunit;
using Social.Pages.Components;

namespace Social.Tests.Components;

public class RichTextTests : ComponentTestBase
{
    private IReadOnlyList<(string Href, string Text)> Links(string text) =>
        Render<RichText>(p => p.Add(c => c.Text, text))
            .FindAll("a.rich-link")
            .Select(a => (a.GetAttribute("href")!, a.TextContent))
            .ToList();

    [Fact]
    public void LinksHashtagsAndMentions()
    {
        var links = Links("Sunset with @Bob_1 #Beach");

        Assert.Equal(new[] { ("u/bob_1", "@Bob_1"), ("tags/beach", "#Beach") }, links);
    }

    [Fact]
    public void TrailingDotIsNotPartOfTheMention()
    {
        var cut = Render<RichText>(p => p.Add(c => c.Text, "Thanks @lucas."));

        var link = cut.Find("a.rich-link");
        Assert.Equal("u/lucas", link.GetAttribute("href"));
        Assert.Equal("@lucas", link.TextContent);
        Assert.EndsWith("@lucas.", cut.Markup.Replace("</a>", ""));
    }

    [Theory]
    [InlineData("mail me: anna@example.com")]
    [InlineData("too short @ab")]
    [InlineData("no tags here")]
    public void IgnoresEmailsShortNamesAndPlainText(string text) =>
        Assert.Empty(Links(text));

    [Fact]
    public void KeepsSurroundingTextAndEncodesHtml()
    {
        var cut = Render<RichText>(p => p.Add(c => c.Text, "<b>hi</b> @bob"));

        Assert.Contains("&lt;b&gt;hi&lt;/b&gt;", cut.Markup);
        Assert.Single(cut.FindAll("a.rich-link"));
    }
}
