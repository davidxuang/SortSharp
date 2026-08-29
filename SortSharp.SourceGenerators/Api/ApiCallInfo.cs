using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.Foundation;

namespace SortSharp.SourceGenerators.Api;

internal sealed class ApiCallInfo
{
    private readonly HashSet<string> _dataCalls = [];
    private readonly Dictionary<string, FromInfo> _from = [];

    public bool HasFrom => _from.Count != 0;

    public ApiCallInfo(MethodDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var target = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            bool templated = HasTemplate(target)
                || HasTemplate(target?.OriginalDefinition)
                || HasTemplate(target?.ReducedFrom)
                || HasTemplate(target?.ReducedFrom?.OriginalDefinition);
            bool dispatcher = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .Any(n => n.Identifier.Text == nameof(Dispatcher<>));
            bool sourceImplementation = target is not null
                && SymbolEqualityComparer.Default.Equals(
                    target.ContainingAssembly, semanticModel.Compilation.Assembly);

            // Calls into implementations produced by another generator are unresolved in this
            // generator's input compilation. A bare span argument is the controlled-source
            // convention that identifies those data calls.
            if (!templated && !dispatcher && !sourceImplementation && target is not null)
                continue;
            if (!invocation.ArgumentList.Arguments.Any(a =>
                a.Expression is IdentifierNameSyntax { Identifier.Text: "span" }))
                continue;

            string key = invocation.WithoutTrivia().ToFullString();
            _dataCalls.Add(key);
            var from = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .FirstOrDefault(n => n is
                {
                    Identifier.Text: "From",
                    TypeArgumentList.Arguments.Count: 2
                });
            if (from is null)
                continue;

            var keyContainer = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .LastOrDefault(n => n.SpanStart < from.SpanStart && n.TypeArgumentList.Arguments.Count == 1)
                ?? throw new InvalidOperationException("Cannot determine the final key type of From<,>.");

            _from.Add(key, new(
                from.SpanStart,
                from.TypeArgumentList.Arguments[0],
                from.TypeArgumentList.Arguments[1],
                keyContainer.TypeArgumentList.Arguments[0]));
        }
    }

    private static bool HasTemplate(IMethodSymbol? method)
        => method?.GetAttributes().Any(a =>
            a.AttributeClass?.Name == nameof(ImplTemplateAttribute)) == true;

    public bool IsDataCall(InvocationExpressionSyntax invocation)
        => _dataCalls.Contains(invocation.WithoutTrivia().ToFullString());

    public bool TryGetFrom(InvocationExpressionSyntax invocation, out FromInfo info)
    {
        if (_from.TryGetValue(invocation.WithoutTrivia().ToFullString(), out info))
            return true;

        var from = invocation.Expression.DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .FirstOrDefault(n => n is { Identifier.Text: "From", TypeArgumentList.Arguments.Count: 2 });
        var keyContainer = from is null ? null : invocation.Expression.DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .LastOrDefault(n => n.SpanStart < from.SpanStart
                && n.TypeArgumentList.Arguments.Count == 1);
        if (from is null || keyContainer is null)
        {
            info = default;
            return false;
        }

        info = new(from.SpanStart, from.TypeArgumentList.Arguments[0],
            from.TypeArgumentList.Arguments[1], keyContainer.TypeArgumentList.Arguments[0]);
        return true;
    }

    internal readonly record struct FromInfo(
        int SpanStart,
        TypeSyntax IntermediateType,
        TypeSyntax SelectorType,
        TypeSyntax KeyType);
}
