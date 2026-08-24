using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SortSharp.SourceGeneration;

internal class TrimmingRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var documentations = node.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax ?? throw new InvalidOperationException();
        if (documentations.Any())
            node = node.WithLeadingTrivia(documentations);
        return node;
    }

    public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
    {
        node = node.WithAttributes(SyntaxFactory.SeparatedList(node.Attributes
            .Where(a => !a.Name.ToString().Contains("Template"))));
        return node.Attributes.Count == 0 ? null : node;
    }

    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        token = base.VisitToken(token);
        if (token.HasLeadingTrivia)
            token = token.WithLeadingTrivia(TrimComments(token.LeadingTrivia));
        if (token.HasTrailingTrivia)
            token = token.WithTrailingTrivia(TrimComments(token.TrailingTrivia));
        return token;
    }

    private IEnumerable<SyntaxTrivia> TrimComments(IEnumerable<SyntaxTrivia> trivias)
    {
        if (!trivias.Any(IsComment)) return trivias;

        var first = trivias.First(IsComment);
        var last = trivias.Last(IsComment);

        var leading = trivias.TakeWhile(t => t != first);
        var trailing = trivias.SkipWhile(t => t != last).Skip(1);
        
        return leading.Sum(t => t.ToFullString().Length) < trailing.Sum(t => t.ToFullString().Length)
            ? trailing
            : leading;
    }

    private static bool IsComment(SyntaxTrivia trivia)
        => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia);
}
