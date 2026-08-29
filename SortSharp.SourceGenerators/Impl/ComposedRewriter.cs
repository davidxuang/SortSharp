using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SortSharp.SourceGenerators.Impl;

internal sealed class ComposedRewriter(params IEnumerable<CSharpSyntaxRewriter> rewriters) : CSharpSyntaxRewriter
{
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        foreach (var rewriter in rewriters)
        {
            node = rewriter.Visit(node);
        }
        return node;
    }
}
