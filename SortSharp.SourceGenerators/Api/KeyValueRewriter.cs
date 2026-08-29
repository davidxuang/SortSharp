using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.SourceGenerators.Common;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Api;

internal sealed class KeyValueRewriter(
    ApiCallInfo calls,
    string sourceType,
    bool genericSource) : CSharpSyntaxRewriter
{
    private static readonly StatementSyntax _guard = F.ParseStatement(
        "ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));");

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var span = node.ParameterList.Parameters.Single(p => p.Identifier.Text == "span");
        var keys = span.WithIdentifier(F.Identifier("keys").WithTriviaFrom(span.Identifier));
        var items = span
            .WithType(F.GenericName(nameof(Span<>))
                .WithTypeArgumentList(F.TypeArgumentList([F.IdentifierName("V")])))
            .WithModifiers(default)
            .WithIdentifier(F.Identifier("items"));
        node = node.WithParameterList(node.ParameterList.WithParameters(F.SeparatedList(
            node.ParameterList.Parameters.SelectMany(p =>
                p == span ? new[] { keys, items } : [p]))));

        var parameters = node.TypeParameterList?.Parameters ?? default;
        int keyIndex = parameters.IndexOf(parameters.FirstOrDefault(p =>
            p.Identifier.Text == sourceType));
        node = node.WithTypeParameterList(F.TypeParameterList(
            keyIndex >= 0
                ? parameters.Insert(keyIndex + 1, F.TypeParameter("V"))
                : parameters.Add(F.TypeParameter("V"))));

        if (node.Body is not null)
        {
            node = node.WithBody(node.Body.WithStatements(node.Body.Statements.Insert(0, _guard)));
        }
        else if (node.ExpressionBody is not null)
        {
            node = node.WithBody(F.Block(
                    _guard,
                    F.ExpressionStatement(node.ExpressionBody.Expression)))
                .WithExpressionBody(null)
                .WithSemicolonToken(default);
        }

        node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax
            ?? throw new InvalidOperationException();
        node = new IdentifierRewriter(("span", "keys")).Visit(node) as MethodDeclarationSyntax
            ?? throw new InvalidOperationException();
        if (genericSource)
            node = new TypeRewriter(sourceType, "K", true).Visit(node) as MethodDeclarationSyntax
                ?? throw new InvalidOperationException();

        string parametersDoc = string.Join("_", keys.Identifier.Text, items.Identifier.Text);
        return new DocumentationRewriter(parametersDoc, !genericSource).Visit(node);
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (!calls.IsDataCall(node))
            return base.VisitInvocationExpression(node);

        var expression = Visit(node.Expression) as ExpressionSyntax
            ?? throw new InvalidOperationException();
        expression = InsertValueTypeArgument(expression);
        var arguments = node.ArgumentList.Arguments.SelectMany(argument =>
            argument.Expression is IdentifierNameSyntax { Identifier.Text: "span" }
                ? new[]
                {
                    argument,
                    argument.WithExpression(F.IdentifierName("items"))
                }
                : [Visit(argument) as ArgumentSyntax ?? throw new InvalidOperationException()]);
        return node.WithExpression(expression)
            .WithArgumentList(node.ArgumentList.WithArguments(F.SeparatedList(arguments)));
    }

    private ExpressionSyntax InsertValueTypeArgument(ExpressionSyntax expression)
    {
        GenericNameSyntax? name = expression switch
        {
            GenericNameSyntax generic => generic,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic,
            _ => null
        };
        if (name is null)
            return expression;

        int index = name.TypeArgumentList.Arguments.IndexOf(
            name.TypeArgumentList.Arguments.FirstOrDefault(a =>
                a is IdentifierNameSyntax id && id.Identifier.Text == sourceType));
        if (index < 0)
            return expression;

        var replacement = name.WithTypeArgumentList(name.TypeArgumentList.WithArguments(
            name.TypeArgumentList.Arguments.Insert(index + 1, F.IdentifierName("V"))));
        return expression == name
            ? replacement
            : ((MemberAccessExpressionSyntax)expression).WithName(replacement);
    }
}
