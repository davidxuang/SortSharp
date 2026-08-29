using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.SourceGenerators.Common;
using SortSharp.SourceGenerators.Impl;
using SortSharp.SourceGenerators.Impl.Comparison;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

partial class ImplOverloadGenerator
{
    private sealed record ComparisonTemplate(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<InvocationInfo> Invocations,
        TemplateInfo Info,
        Capabilities Capabilities)
        : Template(Declaration, Invocations, Info)
    {
        public static ComparisonTemplate Create(Template template, Capabilities capabilities)
            => new(template.Declaration, template.Invocations, template.Info, capabilities);

        private static readonly TypeDef _ifh = new("SortSharp.Foundation", "IHandler", ["T"], null, null, TypeKind.Interface);
        private static readonly TypeDef _nmh = new("SortSharp.Foundation", "NumberHandler", ["T"], null, _ifh);
        private static readonly TypeDef _fph = new("SortSharp.Foundation", "FloatingPointHandler", ["T"], null, _ifh);
        private static readonly TypeDef _cmh = new("SortSharp.Foundation", "ComparableHandler", ["T"], null, _ifh);
        private static readonly TypeDef _dfh = new("SortSharp.Foundation", "DefaultHandler", ["T"], null, _ifh);

        public IEnumerable<GeneratedMethod> Generate()
        {
            var options = Info.Options;

            //if (options!.Enable.HasFlag(OptionalOverloads.LessThanOrEqual))
            //{
            //    CSharpSyntaxRewriter LEW = LessEqualRewriter.Instance; // Less(a, b) => !Less(b, a)
            //    var le = (MethodDeclarationSyntax)LEW.Visit(Declaration);
            //    yield return Variant(le);
            //    var tmpl = this with { Declaration = le, Info = Info with { Options = options with { Enable = 0 } } };
            //    foreach (var m in tmpl.Generate())
            //        yield return m;
            //}

            List<MethodDeclarationSyntax> variants = [Declaration];
            if (options!.ItemNames.Any() && options.KeyValue == OverloadOption.Enable)
            {
                KeyValueRewriter KV = options!.AreItemsSpan // <?> => <T, V>
                    ? KeyValueRewriter.CreateForSpans(Invocations, options.ItemType, options.ItemNames)
                    : KeyValueRewriter.CreateForItems(Invocations, options.ItemType, options.ItemNames);
                var kv = (MethodDeclarationSyntax)KV.Visit(Declaration);
                variants.Add(kv);
                yield return Variant(kv);
            }

            if (options.ComparerName is not null)
            {
                ImmutableArray<MethodDeclarationSyntax> C = [], L = [];

                static TypeDef derive(TypeDef origin, string name, ImmutableArray<string> typeParameters)
                {
                    var bs = origin.Base is { Name: "Fn" or "Cmp" or "Op" }
                        ? origin.Base with { Name = name, TypeParameters = typeParameters }
                        : origin.Base;
                    return origin.Name is "Fn" or "Cmp" or "Op"
                        ? origin with { Name = name, TypeParameters = typeParameters, Base = bs }
                        : new(origin.Namespace, name, typeParameters, origin, null, origin.Kind, SyntaxKind.StaticKeyword);
                }
                var cmp1 = derive(Info.ContainingType, "Cmp", [options.ItemType]);
                var cmp2 = derive(Info.ContainingType, "Cmp", [options.ItemType, "C"]);
                var op1 = derive(Info.ContainingType, "Op", [options.ItemType]);

                if (!options.Disable.HasFlag(ComparerOverloads.IComparable))
                {
                    var CBW = new ComparableRewriter(options.ComparerName); // IComparer<T> => IComparable<T>
                    foreach (var d in variants)
                    {
                        var cb = (MethodDeclarationSyntax)CBW.Visit(d);
                        C = C.Add(cb);
                        yield return Variant(cb, cmp1);
                    }
                }

                if (!options.Disable.HasFlag(ComparerOverloads.TComparer))
                {
                    var CGW = ComparerGenericRewriter.Instance; // Comparison<T> => TComparer
                    foreach (var d in variants)
                        yield return Variant((MethodDeclarationSyntax)CGW.Visit(d), cmp2);
                }

                if (!options.Disable.HasFlag(ComparerOverloads.IComparisonOperators))
                {
                    CSharpSyntaxRewriter LTW = Capabilities.ComparisonOperators
                        ? new LessThanRewriter(options.ComparerName) // compare => operator <
                        : new ComparableRewriter(options.ComparerName); // fallback: IComparer<T> => IComparable<T> [with specialized Less()
                    foreach (var d in variants)
                    {
                        var lt = (MethodDeclarationSyntax)LTW.Visit(d);
                        L = L.Add(lt);
                        yield return Variant(lt, op1);
                    }
                }

                if (!Info.Accessibility.HasFlag(Accessibility.Public))
                    yield break;

                var wrapper = Info.Name.Contains("Sort")
                    ? $"{Info.ContainingType.Containing?.Name ?? Info.ContainingType.Name}{Info.Name}"
                    : Info.Name;

                if (!C.IsDefaultOrEmpty)
                {
                    foreach (var target in C)
                    {
                        var method = target!.WithIdentifier(F.Identifier(wrapper));
                        yield return Wrapper(ToContract(method), _ifh);
                        yield return Wrapper(
                            ToWrapper(method, cmp1, Info.Name, false),
                            _cmh);

                        if (L.IsDefaultOrEmpty)
                            yield return Wrapper(
                                ToWrapper(method, cmp1, Info.Name, false),
                                _nmh);
                        if (options.SortProps?.HasFlag(SortProperties.Stable) != false)
                            yield return Wrapper(
                                ToWrapper(method, cmp1, Info.Name, false),
                                _fph);
                    }
                }

                if (!L.IsDefaultOrEmpty)
                {
                    foreach (var target in L)
                    {
                        var method = target!.WithIdentifier(F.Identifier(wrapper));
                        yield return Wrapper(
                            ToWrapper(method, op1, Info.Name, false),
                            _nmh);

                        if (options.SortProps?.HasFlag(SortProperties.Stable) == false)
                            yield return Wrapper(
                                ToWrapper(method, op1, Info.Name, Info.Name.Contains("Sort")),
                                _fph);
                    }
                }

                if (!options.Disable.HasFlag(ComparerOverloads.TComparer))
                {
                    var CWW = new ComparerWrapperRewriter(options.ComparerName); // strip Comparer for wrappers
                    foreach (var d in variants)
                    {
                        var method = d!.WithIdentifier(F.Identifier(wrapper));
                        yield return Wrapper(
                            (MethodDeclarationSyntax)CWW.Visit(ToWrapper(method, cmp2, Info.Name, false)),
                            _dfh);
                    }
                }
            }
        }

        private static MethodDeclarationSyntax ToContract(MethodDeclarationSyntax method)
            => method.WithModifiers([F.Token(SyntaxKind.AbstractKeyword)])
                .WithAttributeLists([])
                .WithConstraintClauses([])
                .WithBody(null)
                .WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken));

        private static MethodDeclarationSyntax ToWrapper(
            MethodDeclarationSyntax method,
            TypeDefCore targetType,
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
                    .Select(p => F.ParseStatement($"{p.Identifier} = {p.Identifier}.Sub(start, {p.Identifier}.Length);")),
                F.ExpressionStatement(expr),
            ])));
            }
            else
            {
                method = method.WithBody(null).WithExpressionBody(F.ArrowExpressionClause(expr)).WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken));
            }
            return method;
        }
    }

    private static TypeDeclarationSyntax TransformType(TypeDeclarationSyntax decl, TypeDef meta, Capabilities capabilities)
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
