using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.Foundation;
using SortSharp.SourceGenerators.Common;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Api;

internal sealed class KeySelectorRewriter(
    ApiCallInfo calls,
    string sourceType,
    bool genericSource,
    bool composed) : CSharpSyntaxRewriter
{
    private const string ItemType = "__ApiItem";

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var span = node.ParameterList.Parameters.Single(p => p.Identifier.Text == "span");
        node = node.WithParameterList(node.ParameterList.WithParameters(
            node.ParameterList.Parameters.Replace(span,
                span.WithType(F.GenericName(nameof(Span<>))
                    .WithTypeArgumentList(F.TypeArgumentList([F.IdentifierName(ItemType)]))))));
        node = node.WithIdentifier(F.Identifier(
            node.Identifier.LeadingTrivia,
            GetKeySelectorName(node.Identifier.Text),
            node.Identifier.TrailingTrivia));

        var existing = node.TypeParameterList?.Parameters ?? default;
        node = node.WithTypeParameterList(F.TypeParameterList([
            F.TypeParameter(ItemType),
            .. existing,
            F.TypeParameter("TSelector")
        ]));

        TypeSyntax selectedType = genericSource
            ? F.IdentifierName("K")
            : F.ParseTypeName(sourceType);
        node = node.AddConstraintClauses(F.TypeParameterConstraintClause(F.IdentifierName("TSelector"))
            .WithConstraints(F.SeparatedList<TypeParameterConstraintSyntax>([
                F.TypeConstraint(F.GenericName("IKeySelector")
                    .WithTypeArgumentList(F.TypeArgumentList([
                        F.IdentifierName(ItemType), selectedType
                    ])))
            ])));

        node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax
            ?? throw new InvalidOperationException();
        if (genericSource)
            node = new TypeRewriter(sourceType, "K", true).Visit(node) as MethodDeclarationSyntax
                ?? throw new InvalidOperationException();
        node = new TypeRewriter(ItemType, "T", true).Visit(node) as MethodDeclarationSyntax
            ?? throw new InvalidOperationException();

        return new DocumentationRewriter(span.Identifier.Text, !genericSource).Visit(node);
    }

    private static string GetKeySelectorName(string name)
    {
        int index = name.LastIndexOf("Sort", StringComparison.Ordinal);
        return index < 0 ? name + "By" : name.Insert(index + "Sort".Length, "By");
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (composed && calls.TryGetFrom(node, out var composedFrom))
            return RewriteComposed(node, composedFrom);
        if (!calls.IsDataCall(node))
            return base.VisitInvocationExpression(node);

        if (calls.TryGetFrom(node, out var from))
            return RewriteComposed(node, from);
        if (composed || IsDispatcher(node))
            return base.VisitInvocationExpression(node);

        node = base.VisitInvocationExpression(node) as InvocationExpressionSyntax
            ?? throw new InvalidOperationException();
        if (node.Expression is not MemberAccessExpressionSyntax member)
            throw new InvalidOperationException("Expected a member-access algorithm call.");

        var fromName = F.GenericName("From").WithTypeArgumentList(F.TypeArgumentList([
            F.IdentifierName(ItemType),
            F.IdentifierName("TSelector")
        ]));
        return node.WithExpression(member.WithExpression(F.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            member.Expression,
            fromName)));
    }

    private InvocationExpressionSyntax RewriteComposed(
        InvocationExpressionSyntax node,
        ApiCallInfo.FromInfo info)
    {
        var replacement = F.GenericName("From").WithTypeArgumentList(F.TypeArgumentList([
            F.IdentifierName(ItemType),
            F.GenericName("ComposedKeySelector").WithTypeArgumentList(F.TypeArgumentList([
                F.IdentifierName(ItemType),
                info.IntermediateType,
                info.KeyType,
                F.IdentifierName("TSelector"),
                info.SelectorType
            ]))
        ]));
        return new FromRewriter(replacement).Visit(node) as InvocationExpressionSyntax
            ?? throw new InvalidOperationException();
    }

    private static bool IsDispatcher(InvocationExpressionSyntax node)
        => node.Expression.DescendantNodesAndSelf().OfType<GenericNameSyntax>()
            .Any(n => n.Identifier.Text == nameof(Dispatcher<object>));

    private sealed class FromRewriter(GenericNameSyntax replacement)
        : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
            => node is { Identifier.Text: "From", TypeArgumentList.Arguments.Count: 2 }
                ? replacement.WithTriviaFrom(node)
                : base.VisitGenericName(node);
    }
}
