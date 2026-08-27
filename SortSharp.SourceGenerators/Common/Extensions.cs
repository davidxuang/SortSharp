using System.Reflection.Metadata;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

internal static class Extensions
{
    public static bool TestType(this TypeSyntax? type, string? name)
        => (type is IdentifierNameSyntax { Identifier.Text: var i } && i == name)
        || (type is PredefinedTypeSyntax { Keyword.Text: var p } && p == name)
        || (type is RefTypeSyntax { Type: TypeSyntax r } && TestType(r, name))
        || (type is GenericNameSyntax { TypeArgumentList.Arguments: [TypeSyntax g] } && TestType(g, name))
        || (type is NullableTypeSyntax { ElementType: GenericNameSyntax { TypeArgumentList.Arguments: [TypeSyntax n] } } && TestType(n, name));

    public static bool TestParamType(this TypeSyntax? type, string? name)
        => (type is IdentifierNameSyntax { Identifier.Text: var i } && i == name)
        || (type is PredefinedTypeSyntax { Keyword.Text: var p } && p == name)
        || (type is GenericNameSyntax { Identifier.Text: nameof(Span<>) or nameof(ReadOnlySpan<>), TypeArgumentList.Arguments: [TypeSyntax g] } && TestParamType(g, name));

    public static bool HasIdentifierName(this SyntaxNode node, string? name)
    {
        return name is not null && node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id => id.Identifier.Text == name);
    }

    public static bool IsComparisonOperator(this SyntaxToken token) =>
        token.IsKind(SyntaxKind.LessThanToken)
        || token.IsKind(SyntaxKind.LessThanEqualsToken)
        || token.IsKind(SyntaxKind.GreaterThanToken)
        || token.IsKind(SyntaxKind.GreaterThanEqualsToken)
        || token.IsKind(SyntaxKind.EqualsEqualsToken)
        || token.IsKind(SyntaxKind.ExclamationEqualsToken);

    public static bool HasComparisonOperator(this SyntaxNode node)
    {
        return node.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Any(b =>
            b.OperatorToken.IsComparisonOperator());
    }

    public static T[] Squeeze<T>(this IEnumerable<T> nodes) where T : SyntaxNode
    {
        var arr = nodes.ToArray();
        if (arr.Length == 2)
        {
            var a = arr[0];
            var b = arr[1];

            if (a.HasTrailingTrivia && b.HasLeadingTrivia)
            {
                var x = a.GetTrailingTrivia().ToFullString();
                var y = b.GetLeadingTrivia().ToFullString();

                if (x.Contains("\n") && y.Contains("\n"))
                {
                    return [a.WithoutTrailingTrivia(), b];
                }
            }
        }
        return arr;
    }

    public static SyntaxNode SingleWithBlock(this IEnumerable<StatementSyntax> statements)
    {
        return statements.Count() > 1
            ? F.Block(statements.Squeeze())
            : statements.Single();
    }
}
