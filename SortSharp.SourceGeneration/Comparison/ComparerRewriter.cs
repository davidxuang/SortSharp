using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGeneration.Comparison;

internal sealed class ComparerRewriter : CSharpSyntaxRewriter
{
    internal static ComparerRewriter Instance { get; } = new();
    private static readonly SyntaxToken _type = F.Identifier(nameof(IComparer<>));

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        return base.VisitGenericName(node.Identifier.Text == nameof(Comparison<>)
            ? node.WithIdentifier(_type.WithTriviaFrom(node.Identifier))
            : node);
    }
}
