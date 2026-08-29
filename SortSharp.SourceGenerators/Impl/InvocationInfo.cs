using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OneOf;
using SortSharp.SourceGenerators.Common;

namespace SortSharp.SourceGenerators.Impl;

internal sealed record InvocationInfo(
    int ContentHash,
    string Name,
    InvocationExpansion Expansion,
    int ComparerInsertion,
    bool ExpandTypeArgument)
{
    internal static IEnumerable<OneOf<Diagnostic, InvocationInfo>> Resolve(
        GeneratorAttributeSyntaxContext ctx, MethodDeclarationSyntax decl, CancellationToken ct)
    {
        return decl.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .SelectMany(inv =>
            {
                var hash = inv.GetContentHashCode();
                var name = inv.GetMethodName();
                var op = ctx.SemanticModel.GetOperation(inv, ct);
                OneOf<Exception, InvocationExpansion> exp = InvocationExpansion.None;

                int comp = -1;
                bool expand = false;

                if (op is IInvocationOperation io)
                {
                    var target = io.TargetMethod;
                    exp = InvocationExpansion.Resolve(target);
                    if (inv.Expression is GenericNameSyntax)
                        expand = target.GetAttributeOf<TypeArgumentExpansionAttribute>() is not null;
                }
                else if (op is IInvalidOperation && !inv.HasSimpleName(nameof(Debug)))
                {
                    var anchorType = ResolveReceiverAnchor(inv, ctx.SemanticModel, ct)
                        ?? ((IMethodSymbol)ctx.TargetSymbol).ContainingType;

                    if (anchorType?.GetAttributeOf<GenerateInlineArrayAttribute>() is not null &&
                        name == InlineArrayGenerator.CopyMethodName)
                    {
                        exp = InvocationExpansion.Whole;
                    }
                    else if (anchorType is not null)
                    {
                        IMethodSymbol[] candidates;
                        HashSet<INamedTypeSymbol?> visited = new(SymbolEqualityComparer.Default);
                        var targetType = anchorType.TopType;
                        do
                        {
                            candidates = [.. targetType.NestedTypesAndSelf()
                                .SelectMany(type => type.GetMembers(name))
                                .OfType<IMethodSymbol>()
                                .Where(method => method.GetAttributeOf<ImplTemplateAttribute>() is not null)];
                            if (candidates.Length > 0)
                                break;
                            anchorType = anchorType?.SelfAndNestedTypes()
                                .Concat(targetType.SelfAndNestedTypes())
                                .FirstOrDefault(t => !visited.Contains(t.BaseType) && t.BaseType?.SpecialType == SpecialType.None)
                                ?.BaseType;
                            visited.Add(anchorType);
                            targetType = anchorType?.TopType;
                        } while (targetType?.SpecialType == SpecialType.None);

                        if (candidates.Length > 0)
                        {
                            exp = InvocationExpansion.Resolve(candidates.Length == 1
                                ? candidates.Single()
                                : candidates.Single(method =>
                                    method.Parameters.Length == inv.ArgumentList.Arguments.Count));

                            // try guessing the comparer index when the target method has not been generated yet
                            if (!inv.ArgumentList.Arguments.Any(arg =>
                            {
                                var t = ctx.SemanticModel.GetOperation(arg.Expression, ct)?.Type;
                                return t?.FullName == typeof(Comparison<>).FullName ||
                                    t?.FullName == typeof(IComparer<>).FullName ||
                                    t?.ImplementsInterface(typeof(IComparer<>)) == true;
                            }))
                            {
                                comp = candidates.Select(method => method.Parameters.FindIndex(param =>
                                    param.Type?.FullName == typeof(Comparison<>).FullName ||
                                    param.Type?.FullName == typeof(IComparer<>).FullName ||
                                    param.Type?.ImplementsInterface(typeof(IComparer<>)) == true) + 1)
                                    .SingleOrDefault(i => i > 0) - 1;
                            }
                        }
                        else
                            throw new InvalidOperationException("Cannot resolve target method");
                    }
                }
                else
                    return Enumerable.Empty<OneOf<Diagnostic, InvocationInfo>>();

                return [exp
                    .MapT1(r => new InvocationInfo(hash, name, r, comp, expand))
                    .MapT0(l => Diagnostic.Create(DiagnosticDescriptors.Exception, inv.GetLocation(), l.GetType().FullName, name))];
            });
    }

    static INamedTypeSymbol? ResolveReceiverAnchor(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
            return null;
        ExpressionSyntax? expression = member.Expression;
        while (expression is not null)
        {
            if (ResolveType(expression, semanticModel, ct) is INamedTypeSymbol type)
                return type;

            expression = expression switch
            {
                MemberAccessExpressionSyntax access => access.Expression,
                _ => null
            };
        }
        return null;
    }

    static INamedTypeSymbol? ResolveType(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var type = semanticModel.GetSymbolInfo(expression, ct).Symbol switch
        {
            INamedTypeSymbol t => t,
            IAliasSymbol { Target: INamedTypeSymbol t } => t,
            ILocalSymbol { Type: INamedTypeSymbol t } => t,
            IFieldSymbol { Type: INamedTypeSymbol t } => t,
            IPropertySymbol { Type: INamedTypeSymbol t } => t,
            IParameterSymbol { Type: INamedTypeSymbol t } => t,
            _ => null
        } ?? semanticModel.GetTypeInfo(expression, ct).Type as INamedTypeSymbol;
        return type is not IErrorTypeSymbol ? type : null;
    }
    
    public readonly struct EffectComparer : IEqualityComparer<InvocationInfo>
    {
        public bool Equals(InvocationInfo x, InvocationInfo y)
            => x.Expansion.Equals(y.Expansion) && x.ComparerInsertion == y.ComparerInsertion;
        public int GetHashCode(InvocationInfo obj)
            => HashCode.Combine(obj.Expansion, obj.ComparerInsertion);
    }
}
