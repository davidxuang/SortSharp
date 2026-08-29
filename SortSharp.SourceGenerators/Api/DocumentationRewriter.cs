using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SortSharp.SourceGenerators.Api;

internal sealed class DocumentationRewriter(
    string parameter,
    bool insertTypeParams) : CSharpSyntaxRewriter(visitIntoStructuredTrivia: true)
{
    private string _typeParams = "";

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        _typeParams = string.Join("_",
            node.TypeParameterList?.Parameters.Select(p => p.Identifier.Text) ?? []);
        return base.VisitMethodDeclaration(node);
    }

    public override SyntaxNode? VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
    {
        var content = new List<XmlNodeSyntax>();
        bool hasTypeParams = false;
        foreach (var original in node.Content)
        {
            XmlNodeSyntax item = original;
            if (item is XmlEmptyElementSyntax xml && TryGetPath(xml, out var path))
            {
                if (path.Contains("TypeParams/Member", StringComparison.Ordinal))
                {
                    hasTypeParams = true;
                    xml = SetPath(xml, ReplaceMember(path, _typeParams));
                }
                else if (path.Contains("Params/Member[@name=\"span\"]", StringComparison.Ordinal))
                {
                    if (insertTypeParams && !hasTypeParams)
                    {
                        content.Add(SetPath(xml, path
                            .Replace("Params/Member", "TypeParams/Member")
                            .Replace("[@name=\"span\"]", $"[@name=\"{_typeParams}\"]")));
                        hasTypeParams = true;
                    }
                    xml = SetPath(xml, path.Replace(
                        "Params/Member[@name=\"span\"]",
                        $"Params/Member[@name=\"{parameter}\"]"));
                }
                item = xml;
            }
            content.Add(item);
        }
        return node.WithContent(SyntaxFactory.List(content));
    }

    private static string ReplaceMember(string path, string name)
    {
        int start = path.IndexOf("[@name=\"", StringComparison.Ordinal);
        if (start < 0) return path;
        start += "[@name=\"".Length;
        int end = path.IndexOf("\"]", start, StringComparison.Ordinal);
        return end < 0 ? path : path[..start] + name + path[end..];
    }

    private static bool TryGetPath(XmlEmptyElementSyntax xml, out string path)
    {
        var attribute = xml.Attributes.OfType<XmlTextAttributeSyntax>()
            .FirstOrDefault(a => a.Name.LocalName.Text == "path");
        path = attribute is null
            ? ""
            : string.Concat(attribute.TextTokens.Select(t => t.ValueText));
        return attribute is not null;
    }

    private static XmlEmptyElementSyntax SetPath(XmlEmptyElementSyntax xml, string path)
    {
        return xml.WithAttributes(SyntaxFactory.List(xml.Attributes.Select(a =>
            a is XmlTextAttributeSyntax text && text.Name.LocalName.Text == "path"
                ? text.WithTextTokens(SyntaxFactory.TokenList(
                    SyntaxFactory.XmlTextLiteral(path)))
                : a)));
    }
}
