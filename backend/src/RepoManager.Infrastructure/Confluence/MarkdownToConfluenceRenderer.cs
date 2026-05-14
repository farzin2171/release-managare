using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace RepoManager.Infrastructure.Confluence;

/// <summary>
/// Converts Markdown to Confluence Storage Format (XHTML-based).
/// Standard HTML elements (headings, bold, italic, inline code, links, lists) are
/// valid in Confluence Storage Format unchanged. Only fenced code blocks require
/// the Confluence-specific ac:structured-macro rendering.
/// </summary>
public static class MarkdownToConfluenceConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string Convert(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;
        var writer = new StringWriter();
        var renderer = new MarkdownToConfluenceRenderer(writer);
        var document = Markdown.Parse(markdown, Pipeline);
        renderer.Render(document);
        return writer.ToString();
    }
}

internal sealed class MarkdownToConfluenceRenderer : HtmlRenderer
{
    public MarkdownToConfluenceRenderer(TextWriter writer) : base(writer)
    {
        ObjectRenderers.RemoveAll(r => r is CodeBlockRenderer);
        ObjectRenderers.Add(new ConfluenceCodeBlockRenderer());
    }
}

internal sealed class ConfluenceCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
{
    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        var code = obj.Lines.ToString().TrimEnd();
        var language = (obj as FencedCodeBlock)?.Info?.Trim() ?? string.Empty;

        renderer.Write("<ac:structured-macro ac:name=\"code\">");
        if (!string.IsNullOrEmpty(language))
            renderer.Write($"<ac:parameter ac:name=\"language\">{language}</ac:parameter>");
        renderer.Write("<ac:plain-text-body><![CDATA[");
        renderer.Write(code);
        renderer.Write("]]></ac:plain-text-body>");
        renderer.Write("</ac:structured-macro>\n");
    }
}
