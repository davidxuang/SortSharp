using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Common;

internal sealed class IdentifierRewriter(params IEnumerable<(string, string)> map) : CSharpSyntaxRewriter
{
    internal readonly Dictionary<string, string> Map = map.ToDictionary(t => t.Item1, t => t.Item2);

    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        return base.VisitParameter(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n).WithTriviaFrom(node.Identifier))
            : node);
    }

    public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        return base.VisitVariableDeclarator(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n).WithTriviaFrom(node.Identifier))
            : node);
    }

    public override SyntaxNode? VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
    {
        return base.VisitSingleVariableDesignation(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n).WithTriviaFrom(node.Identifier))
            : node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        return base.VisitIdentifierName(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n)).WithTriviaFrom(node)
            : node);
    }
}
