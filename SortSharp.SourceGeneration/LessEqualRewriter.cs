using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGeneration;

internal sealed class LessEqualRewriter : CSharpSyntaxRewriter
{
    public static readonly LessEqualRewriter Instance = new();

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        return base.VisitMethodDeclaration(node.WithIdentifier(F.Identifier($"{node.Identifier.Text}LE")));
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        return node.Expression switch
        {
            IdentifierNameSyntax { Identifier.Text: "Less" } => F.PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                F.ParenthesizedExpression(
                    node.WithArgumentList(F.ArgumentList([
                        node.ArgumentList.Arguments[1],
                        node.ArgumentList.Arguments[0],
                        .. node.ArgumentList.Arguments.Skip(2)])))),
            IdentifierNameSyntax id when id.Identifier.Text is not "Swap" => node.WithExpression(id.WithIdentifier(F.Identifier($"{id.Identifier.Text}LE"))),
            _ => base.VisitInvocationExpression(node)
        };
    }

    public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        return node.IsKind(SyntaxKind.LogicalNotExpression)
            && node.Operand is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Less" } } ie
            ? ie.WithArgumentList(F.ArgumentList([
                ie.ArgumentList.Arguments[1],
                ie.ArgumentList.Arguments[0],
                .. ie.ArgumentList.Arguments.Skip(2) ]))
            : base.VisitPrefixUnaryExpression(node);
    }
}
