using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.SourceGenerators.Comparison;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

partial class OverloadGenerator
{
    private sealed record ComparisonOptions(
        string GenericName,
        string? CompareName,
        ImmutableArray<string> ItemNames,
        bool IsItemsSpan,
        SortProperties Properties,
        DefaultOverloads Disable,
        OptionalOverloads Enable);

    private static readonly TypeDecl _ifh = new("SortSharp.Foundation", "IHandler", ["T"], null, null, TypeKind.Interface);
    private static readonly TypeDecl _nmh = new("SortSharp.Foundation", "NumberHandler", ["T"], null, _ifh);
    private static readonly TypeDecl _fph = new("SortSharp.Foundation", "FloatingPointHandler", ["T"], null, _ifh);
    private static readonly TypeDecl _cmh = new("SortSharp.Foundation", "ComparableHandler", ["T"], null, _ifh);
    private static readonly TypeDecl _dfh = new("SortSharp.Foundation", "DefaultHandler", ["T"], null, _ifh);

    private static MethodDeclarationSyntax ToContract(MethodDeclarationSyntax method)
        => method.WithModifiers([F.Token(SyntaxKind.AbstractKeyword)])
            .WithAttributeLists([])
            .WithConstraintClauses([])
            .WithBody(null)
            .WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken));

    private static MethodDeclarationSyntax ToWrapper(
        MethodDeclarationSyntax method,
        TypeDeclCore targetType,
        string targetMethod,
        bool moveNans)
    {
        method = method.WithModifiers([F.Token(SyntaxKind.PublicKeyword)])
            .WithAttributeLists([])
            .WithConstraintClauses([]);
        var expr = F.InvocationExpression(
            F.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetType.Syntax, F.IdentifierName(targetMethod)),
            F.ArgumentList([.. method.ParameterList.Parameters.Select(p => F.Argument(F.IdentifierName(p.Identifier)))]));
        if (moveNans)
        {
            method = method.WithBody(F.Block(F.List<StatementSyntax>([
                F.LocalDeclarationStatement(
                    [],
                    F.VariableDeclaration(F.PredefinedType(F.Token(SyntaxKind.IntKeyword)), [
                        F.VariableDeclarator(F.Identifier("start"))
                            .WithInitializer(F.EqualsValueClause(
                                F.InvocationExpression(
                                F.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, F.IdentifierName("SpanOperations"), F.IdentifierName("MoveNansToFront")),
                                F.ArgumentList([.. method.ParameterList.Parameters
                                    .TakeWhile(p => p.Type is GenericNameSyntax { Identifier.Text: nameof(Span<>) })
                                    .Select(p => F.Argument(F.IdentifierName(p.Identifier)))]))))
                    ])
                ),
                .. method.ParameterList.Parameters.TakeWhile(p => p.Type is GenericNameSyntax { Identifier.Text: nameof(Span<>) })
                    .Select(p => F.ExpressionStatement(F.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        F.IdentifierName(p.Identifier),
                        F.InvocationExpression(
                            F.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                F.IdentifierName(p.Identifier),
                                F.IdentifierName("Sub")),
                            F.ArgumentList([
                                F.Argument(F.IdentifierName("start")),
                                F.Argument(F.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    F.IdentifierName(p.Identifier),
                                    F.IdentifierName(nameof(Span<>.Length))))
                            ])
                        )))),
                F.ExpressionStatement(expr),
            ])));
        }
        else
        {
            method = method.WithBody(null).WithExpressionBody(F.ArrowExpressionClause(expr)).WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken));
        }
        return method;
    }

    private static IEnumerable<GeneratedMethod> GenerateMethods(
        TemplateMethod<ComparisonOptions> template,
        Capabilities capabilities,
        IDictionary<string, CallSiteBehaviors> behaviors,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var decl = template.Declaration;
        var options = template.Options;

        if (options!.Enable.HasFlag(OptionalOverloads.LessThanOrEqual))
        {
            CSharpSyntaxRewriter LEW = LessEqualRewriter.Instance; // Less(a, b) => !Less(b, a)
            var le = (MethodDeclarationSyntax)LEW.Visit(decl);
            yield return GeneratedMethod.Create(template, le);
            var tmpl = template with { Declaration = le, Options = options with { Enable = 0 } };
            foreach (var m in GenerateMethods(tmpl, capabilities, behaviors, ct))
                yield return m;
        }

        ImmutableArray<MethodDeclarationSyntax> D = [decl];
        if (options.ItemNames.Any() && !options.Disable.HasFlag(DefaultOverloads.KeyValue))
        {
            ItemsRewriter IW = options!.IsItemsSpan // <?> => <T, V>
                ? ItemsRewriter.CreateForSpans(behaviors, options.GenericName, options.ItemNames)
                : ItemsRewriter.CreateForItems(behaviors, options.GenericName, options.ItemNames);
            var iw = (MethodDeclarationSyntax)IW.Visit(decl);
            D = D.Add(iw);
            yield return GeneratedMethod.Create(template, iw);
        }

        if (options.CompareName is not null)
        {
            ImmutableArray<MethodDeclarationSyntax> C = [], L = [];

            static TypeDecl derive(TypeDecl origin, string name, ImmutableArray<string> typeParameters)
            {
                var bs = origin.Base is { Name: "Fn" or "Cmp" or "Op" }
                    ? origin.Base with { Name = name, TypeParameters = typeParameters }
                    : origin.Base;
                return origin.Name is "Fn" or "Cmp" or "Op"
                    ? origin with { Name = name, TypeParameters = typeParameters, Base = bs }
                    : new(origin.Namespace, name, typeParameters, origin, null, origin.Kind, SyntaxKind.StaticKeyword);
            }
            var cmp1 = derive(template.OriginType, "Cmp", [options.GenericName]);
            var cmp2 = derive(template.OriginType, "Cmp", [options.GenericName, "C"]);
            var op1 = derive(template.OriginType, "Op", [options.GenericName]);

            if (!options.Disable.HasFlag(DefaultOverloads.IComparable))
            {
                var CBW = new ComparableRewriter(options.CompareName); // IComparer<T> => IComparable<T>
                foreach (var d in D)
                {
                    var cb = (MethodDeclarationSyntax)CBW.Visit(d);
                    C = C.Add(cb);
                    yield return GeneratedMethod.Create(template, cb, cmp1);
                }
            }

            if (!options.Disable.HasFlag(DefaultOverloads.TComparer))
            {
                var CGW = ComparerGenericRewriter.Instance; // Comparison<T> => TComparer
                foreach (var d in D)
                    yield return GeneratedMethod.Create(template, (MethodDeclarationSyntax)CGW.Visit(d), cmp2);
            }

            if (!options.Disable.HasFlag(DefaultOverloads.IComparisonOperators))
            {
                CSharpSyntaxRewriter LTW = capabilities.ComparisonOperators
                    ? new LessThanRewriter(options.CompareName) // compare => operator <
                    : new ComparableRewriter(options.CompareName); // fallback: IComparer<T> => IComparable<T> [with specialized Less()
                foreach (var d in D)
                {
                    var lt = (MethodDeclarationSyntax)LTW.Visit(d);
                    L = L.Add(lt);
                    yield return GeneratedMethod.Create(template, lt, op1);
                }
            }

            if (!template.Accessibility.HasFlag(Accessibility.Public))
                yield break;

            var wrapper = template.Name.StartsWith("Sort")
                ? $"{template.OriginType.Containing?.Name ?? template.OriginType.Name}{template.Name}"
                : template.Name;

            if (!C.IsDefaultOrEmpty)
            {
                foreach (var target in C)
                {
                    var method = target!.WithIdentifier(F.Identifier(wrapper));
                    yield return GeneratedMethod.CreateWrapper(template, ToContract(method), _ifh);
                    yield return GeneratedMethod.CreateWrapper(
                        template,
                        ToWrapper(method, cmp1, template.Name, false),
                        _cmh);

                    if (L.IsDefaultOrEmpty)
                        yield return GeneratedMethod.CreateWrapper(template,
                            ToWrapper(method, cmp1, template.Name, false),
                            _nmh);
                    if (options.Properties.HasFlag(SortProperties.Stable))
                        yield return GeneratedMethod.CreateWrapper(
                            template,
                            ToWrapper(method, cmp1, template.Name, false),
                            _fph);
                }
            }

            if (!L.IsDefaultOrEmpty)
            {
                foreach (var target in L)
                {
                    var method = target!.WithIdentifier(F.Identifier(wrapper));
                    yield return GeneratedMethod.CreateWrapper(template,
                        ToWrapper(method, op1, template.Name, false),
                        _nmh);

                    if (!options.Properties.HasFlag(SortProperties.Stable))
                        yield return GeneratedMethod.CreateWrapper(template,
                            ToWrapper(method, op1, template.Name, template.Name.StartsWith("Sort")),
                            _fph);
                }
            }

            if (!options.Disable.HasFlag(DefaultOverloads.TComparer))
            {
                var CWW = new ComparerWrapperRewriter(options.CompareName); // strip Comparer for wrappers
                foreach (var d in D)
                {
                    var method = d!.WithIdentifier(F.Identifier(wrapper));
                    yield return GeneratedMethod.CreateWrapper(template,
                        (MethodDeclarationSyntax)CWW.Visit(ToWrapper(method, cmp2, template.Name, false)),
                        _dfh);
                }
            }
        }
    }

    private static TypeDeclarationSyntax TransformSyntax(TypeDeclarationSyntax decl, TypeDecl meta, Capabilities capabilities)
    {
        return meta switch
        {
            { Name: "Cmp", TypeParameters: [var T, var C] } => decl.AddConstraintClauses([
                F.TypeParameterConstraintClause(
                                    F.IdentifierName(C),
                                    [ F.TypeConstraint(F.ParseTypeName($"global::System.Collections.Generic.IComparer<{T}>")) ])
            ]),
            { Name: "Cmp", TypeParameters: [var T] } => decl.AddConstraintClauses([
                F.TypeParameterConstraintClause(
                                    F.IdentifierName(T),
                                    [ F.TypeConstraint(F.ParseTypeName($"global::System.IComparable<{T}>")) ])
            ]),
            { Name: "Op", TypeParameters: [var T] } => decl.AddConstraintClauses([
                F.TypeParameterConstraintClause(
                                    F.IdentifierName(T),
                                    [
                                        F.TypeConstraint(F.IdentifierName("unmanaged")),
                                        capabilities.ComparisonOperators
                                            ? F.TypeConstraint(F.ParseTypeName($"global::System.Numerics.IComparisonOperators<{T}, {T}, bool>"))
                                            : F.TypeConstraint(F.ParseTypeName($"global::System.IComparable<{T}>"))
                                    ])
            ]),
            _ => decl
        };
    }
}
