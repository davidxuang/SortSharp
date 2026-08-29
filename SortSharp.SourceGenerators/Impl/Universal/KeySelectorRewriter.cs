using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.SourceGenerators.Common;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Impl.Universal;

internal class KeySelectorRewriter(
    IEnumerable<InvocationInfo> infos,
    string typeName) : CSharpSyntaxRewriter
{
    private readonly ComposedRewriter _V = new(
        new TypeRewriter(typeName, "V"),
        new TypeRewriter("__T", typeName), // recover the protected `T`
        new StackallocRewriter("V"));
    // use placeholder `__T` to protect the original type parameter `T`
    private readonly TypeRewriter _T = new(typeName, "__T", true);

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax ?? throw new InvalidOperationException();
        if (node.TypeParameterList?.Parameters is [var tp] && tp.Identifier.Text == typeName)
            node = node.WithTypeParameterList(null);
        if (node.ConstraintClauses is [{ Name: var tpcn }] && tpcn.Identifier.Text == typeName)
            node = node.WithConstraintClauses([]);
        return _V.Visit(node);
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.DescendantNodes().OfType<GenericNameSyntax>()
            .Any(gn => gn is { Identifier.Text: "Cmp" or "Op", TypeArgumentList.Arguments: [var v, ..] } && v.HasSimpleName(typeName)))
        {
            var match = infos.Match(node);
            if (match is not null)
            {
                if (match.ComparerInsertion >= 0)
                {
                    var args = node.ArgumentList.Arguments.ToList();
                    args.Insert(match.ComparerInsertion, F.Argument(F.IdentifierName("Comparer")));
                    node = node.WithArgumentList(F.ArgumentList([.. args]));
                }
            }
            else
                throw new InvalidOperationException("Unable to find a position to insert the comparer");
        }
        else if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax cl } ma
            && cl.Identifier.Text is "SpanOperations")
        {
            node = node.WithExpression(ma.WithExpression(F.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                cl,
                (SimpleNameSyntax)F.ParseName($"From<V, __T, TSelector>"))));
        }
        return base.VisitInvocationExpression(node);
    }

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        return node switch
        {
            { Identifier.Text: "Cmp" or "Op", TypeArgumentList.Arguments: [var v] } when v.HasSimpleName(typeName)
                => F.GenericName(F.Identifier("Cmp"),
                    F.TypeArgumentList([F.IdentifierName("V"), F.ParseTypeName("Foundation.KeySelectorComparer<V, __T, TSelector>")])),
            {
                Identifier.Text: "Cmp",
                TypeArgumentList.Arguments: [var v, GenericNameSyntax c]
            } when v.HasSimpleName(typeName)
                => F.GenericName(F.Identifier("Cmp"),
                    F.TypeArgumentList([F.IdentifierName("V"), F.ParseTypeName($"Foundation.KeySelectorComparer<V, __T, TSelector, {_T.Visit(c).ToFullString()}>")])),
            _ => base.VisitGenericName(node),
        };
    }
}
