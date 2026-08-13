using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGeneration.Templates;

[Generator]
internal sealed class TemplateMethodGenerator : IIncrementalGenerator
{
    private record struct Capabilities(bool UsesLessThan);

    private static readonly CSharpSyntaxRewriter trimmer = new TrimmingRewriter();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var templates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{typeof(TemplateAttribute).Namespace}.{nameof(TemplateAttribute)}",
                static (node, _) => node is MemberDeclarationSyntax,
                static (ctx, ct) => AnalyzeTemplate(ctx, ct));

        var capabilities = context.CompilationProvider
            .Select(static (cmpl, _) =>
                new Capabilities(
                    UsesLessThan: cmpl.GetTypeByMetadataName(
                        "System.Numerics.IComparisonOperators`3") is not null));

        var behaviors = templates
            .Select(static (tmpl, _) => (tmpl.Name, tmpl.Behavior))
            .Where(static pair => pair.Behavior != CallSiteBehaviors.None)
            .Collect()
            .Select(static (pairs, _) => BuildBehaviors(pairs))
            .WithComparer(BehaviorMapComparer.Instance);

        var generated = templates
            .Where(static tmpl => tmpl.Options is not null)
            .Combine(capabilities)
            .Combine(behaviors)
            .SelectMany(static (tuple, ct) =>
            {
                var template = tuple.Left.Left;
                var behaviors = tuple.Right;
                var capabilities = tuple.Left.Right;

                return GenerateMethods(template, capabilities, behaviors, ct)
                    .ToImmutableArray();
            });

        var files = generated
            .Collect()
            .Combine(capabilities)
            .Select(static (tuple, _) =>
                RenderFiles(tuple.Left, tuple.Right));

        context.RegisterSourceOutput(
            files,
            static (spc, files) =>
            {
                foreach (var file in files)
                    spc.AddSource(file.FilePath, file.Source);
            });
    }

    private record class TypeDeclCore(
        string Name,
        ImmutableArray<string> TypeParameters,
        TypeDeclCore? Containing = null)
    {
        private string Hint => TypeParameters.Length == 0
            ? Name
            : $"{Name}_{TypeParameters.Length}";
        public string HintName => Containing is not null
            ? $"{Containing.HintName}.{Hint}"
            : Hint;
        private SimpleNameSyntax BaseSyntax => TypeParameters.Length == 0
            ? F.IdentifierName(Name)
            : F.GenericName(Name).WithTypeArgumentList(
                F.TypeArgumentList([.. TypeParameters.Select(F.IdentifierName)]));
        public NameSyntax Syntax => Containing is not null
            ? F.QualifiedName(Containing.Syntax, BaseSyntax)
            : BaseSyntax;
    }
    private sealed record class TypeDecl(
        string Name,
        ImmutableArray<string> TypeParameters,
        TypeDeclCore? Containing = null,
        TypeDeclCore? Base = null,
        TypeKind Kind = TypeKind.Class) : TypeDeclCore(Name, TypeParameters, Containing);

    private static TypeDecl ResolveType(ITypeSymbol symbol)
        => new(symbol.Name,
            symbol is INamedTypeSymbol named
                ? [.. named.TypeParameters.Select(p => p.Name)]
                : [],
            symbol.ContainingType is not null
                ? ResolveType(symbol.ContainingType)
                : null,
            symbol.BaseType is not null && symbol.BaseType.SpecialType != SpecialType.System_Object
                ? ResolveType(symbol.BaseType)
                : null,
            symbol.IsValueType ? TypeKind.Struct : TypeKind.Class);

    private static T GetNamedArgument<T>(AttributeData? attr, string name, T defaultValue = default!)
    {
        if (attr == null) return defaultValue;
        if (attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is object obj)
            try { return (T)obj; } catch { }
        return defaultValue;
    }

    private sealed record TemplateMethod(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<string> Usings,
        string OriginFolder,
        string OriginNamespace,
        TypeDecl OriginType,
        string Name,
        CallSiteBehaviors Behavior,
        TemplateOptions? Options);

    private sealed record TemplateOptions(
        string GenericName,
        string? CompareName,
        ImmutableArray<string> ItemNames,
        bool IsItemsSpan,
        TemplateVariants Switch,
        bool IsPublic,
        bool IsUnstable);

    private static TemplateMethod AnalyzeTemplate(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var decl = (MethodDeclarationSyntax)ctx.TargetNode;
        var symbol = (IMethodSymbol)ctx.TargetSymbol;
        var attr = ctx.Attributes.Single();
        var rootClass = symbol.ContainingType;
        while (rootClass.ContainingType is not null)
            rootClass = rootClass.ContainingType;
        var attrClass = rootClass.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == nameof(TemplateClassAttribute));

        IEnumerable<string> segments = decl.SyntaxTree.FilePath.Split('\\', '/');
        segments = segments.Reverse().Skip(1).TakeWhile(s => !s.StartsWith("SortSharp")).Reverse();
        var relative = string.Join("/", segments);

        var type = new TypeDecl(symbol.ContainingType.Name,
            symbol.ContainingType is INamedTypeSymbol named
                ? [.. named.TypeParameters.Select(p => p.Name)]
                : [],
            symbol.ContainingType.ContainingType is not null
                ? ResolveType(symbol.ContainingType.ContainingType)
                : null,
            symbol.ContainingType.BaseType is not null && symbol.ContainingType.BaseType.SpecialType != SpecialType.System_Object
                ? ResolveType(symbol.ContainingType.BaseType)
                : null);

        TemplateOptions? options;
        var generic = attr.ConstructorArguments.Length >= 1 ? attr.ConstructorArguments[0].Value as string : null;
        var compare = attr.ConstructorArguments.Length >= 2 ? attr.ConstructorArguments[1].Value as string : null;
        var items = attr.ConstructorArguments.Length >= 3 ? attr.ConstructorArguments[2].Values.Select(d => d.Value).Cast<string>().ToImmutableArray() : [];
        if (string.IsNullOrWhiteSpace(compare) && items.Length == 0)
        {
            // metadata-only without template processing
            options = null;
        }
        else
        {
            options = new TemplateOptions(
                generic ?? throw new InvalidOperationException($"Generic name is missing on {symbol.Name}."),
                compare,
                items,
                IsItemsSpan: symbol.Parameters
                    .Where(p => items.Contains(p.Name))
                    .Any(p => p.Type.Name == nameof(Span<>) || p.Type.Name == nameof(ReadOnlySpan<>)),
                Switch: GetNamedArgument<TemplateVariants>(attrClass, nameof(TemplateClassAttribute.Switch))
                    | GetNamedArgument<TemplateVariants>(attr, nameof(TemplateAttribute.Switch)),
                IsPublic: symbol.DeclaredAccessibility == Accessibility.Public,
                IsUnstable: GetNamedArgument<bool>(attrClass, nameof(TemplateClassAttribute.IsUnstable))
            );
        }

        var behavior = attr.ConstructorArguments.Length < 3 || !attr.ConstructorArguments[2].Values.Any()
            ? CallSiteBehaviors.None
            : attr.ConstructorArguments[2].Values.Select(a => symbol.Parameters.IndexOf(symbol.Parameters.First(p => p.Name == (string)a.Value!)))
                .Select(i => (uint)CallSiteBehaviors.RepeatArgument0 << i)
                .Cast<CallSiteBehaviors>()
                .Aggregate(CallSiteBehaviors.None, (a, b) => a | b);

        return new TemplateMethod(
            decl,
            [.. decl.SyntaxTree.GetCompilationUnitRoot(ct).Usings.Select(u => u.WithoutTrivia().ToFullString())],
            relative.Trim().Trim('/'),
            symbol.ContainingNamespace.ToDisplayString(),
            ResolveType(symbol.ContainingType),
            symbol.Name,
            behavior,
            options);
    }

    private static IDictionary<string, CallSiteBehaviors> BuildBehaviors(ImmutableArray<(string Name, CallSiteBehaviors Behavior)> pairs)
    {
        // presets
        SortedDictionary<string, CallSiteBehaviors> dict = new()
        {
            ["Less"] = CallSiteBehaviors.None,
            ["Swap"] = CallSiteBehaviors.RepeatCall,
            ["SwapBlock"] = CallSiteBehaviors.RepeatCall,
            ["Rotate"] = CallSiteBehaviors.RepeatCall,
            ["CopyTo"] = CallSiteBehaviors.RepeatCall,
            ["CopyToFill"] = CallSiteBehaviors.RepeatCall,
            ["Dispose"] = CallSiteBehaviors.RepeatCall,
        };
        string[] suffices = ["LE"];

        foreach (var (Name, Behavior) in pairs)
        {
            if (dict.TryGetValue(Name, out var b))
            {
                if (Behavior != b)
                    throw new InvalidOperationException($"Conflicting behaviors for method {Name}: {b} vs {Behavior}");
            }
            else
            {
                dict.Add(Name, Behavior);
                foreach (var suffix in suffices)
                    dict.Add($"{Name}{suffix}", Behavior);
            }
        }

        return dict.ToFrozenDictionary(); // order immutability guaranteed by SortedDictionary
    }

    private sealed record GeneratedMethod(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<string> Usings,
        string FilePath,
        string Namespace,
        TypeDecl TypeName)
    {
        public static GeneratedMethod Create(
            TemplateMethod template,
            MemberDeclarationSyntax declaration,
            TypeDecl? child = null)
        {
            var type = child switch
            {
                null => template.OriginType,
                _ => child with { Base = child.Containing is TypeDecl { Base: TypeDeclCore b }
                    ? new(child.Name, child.TypeParameters, b)
                    : null },
            };
            IEnumerable<string> segments = [template.OriginFolder, $"{type.HintName}.g"];
            return new GeneratedMethod(
                (MethodDeclarationSyntax)trimmer.Visit(declaration),
                template.Usings,
                Path.Combine([.. segments.Where(s => !string.IsNullOrEmpty(s))]),
                template.OriginNamespace,
                type);
        }

        public static GeneratedMethod CreateWrapper(
            TemplateMethod template,
            MemberDeclarationSyntax declaration,
            TypeDecl type)
        {
            return new GeneratedMethod(
                (MethodDeclarationSyntax)trimmer.Visit(declaration),
                template.Usings,
                $"{type.HintName}.g",
                template.OriginNamespace.Split('.')[0],
                type);
        }
    }

    private static readonly TypeDecl _idp = new("IDispatcher", ["T"], null, null, TypeKind.Interface);
    private static readonly TypeDecl _ndp = new("NumberDispatcher", ["T"], null, _idp);
    private static readonly TypeDecl _fdp = new("FloatingPointDispatcher", ["T"], null, _idp);
    private static readonly TypeDecl _csp = new("ComparableDispatcher", ["T"], null, _idp);
    private static readonly TypeDecl _dsp = new("DefaultDispatcher", ["T"], null, _idp);

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
                            .WithInitializer(F.EqualsValueClause(F.InvocationExpression(
                                F.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, F.IdentifierName("SortBase"), F.IdentifierName("MoveNansToFront")),
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
        TemplateMethod template,
        Capabilities capabilities,
        IDictionary<string, CallSiteBehaviors> behaviors,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var decl = template.Declaration;
        var options = template.Options;

        if (options!.Switch.HasFlag(TemplateVariants.LessThanOrEqual))
        {
            CSharpSyntaxRewriter LEW = LessEqualRewriter.Instance; // Less(a, b) => !Less(b, a)
            var le = (MethodDeclarationSyntax)LEW.Visit(decl);
            yield return GeneratedMethod.Create(template, le);
            var tmpl = template with { Declaration = le, Options = options with { Switch = options.Switch & ~TemplateVariants.LessThanOrEqual } };
            foreach (var m in GenerateMethods(tmpl, capabilities, behaviors, ct))
                yield return m;
        }

        MethodDeclarationSyntax? iw = null;
        if (options.ItemNames.Any() && !options.Switch.HasFlag(TemplateVariants.KeyValue))
        {
            ItemsRewriter IW = options!.IsItemsSpan // <?> => <T, V>
                ? ItemsRewriter.CreateForSpans(behaviors, options.GenericName, options.ItemNames)
                : ItemsRewriter.CreateForItems(behaviors, options.GenericName, options.ItemNames);
            iw = (MethodDeclarationSyntax)IW.Visit(decl);
            if (template.OriginType.Name is "Cmp" or "Op" && template.OriginType.TypeParameters.Length == 1)
            {
                // T, V => <T>.<V> for Cmp<T>
                iw = iw.WithTypeParameterList(F.TypeParameterList([F.TypeParameter("V")]));
            }
            yield return GeneratedMethod.Create(template, iw);
        }

        if (options.CompareName is not null)
        {
            //if (!options.Skip.HasFlag(TemplateVariant.IComparer))
            //{
            //    var cw = ComparerRewriter.Instance; // Comparison<T> => IComparer<T>
            //    yield return GeneratedMethod.Create(template, (MethodDeclarationSyntax)cw.Visit(decl));
            //    if (I is not null)
            //        yield return GeneratedMethod.Create(template, (MethodDeclarationSyntax)cw.Visit(I));
            //}

            MethodDeclarationSyntax? cb = null, ci = null, lt = null, li = null;
            var parent = template.OriginType.Name is "Fn" or "Cmp" or "Op"
                ? template.OriginType.Containing ?? throw new InvalidOperationException()
                : template.OriginType;
            var cmp1 = new TypeDecl("Cmp", [options.GenericName], parent, (parent as TypeDecl)?.Base);
            var cmp2 = new TypeDecl("Cmp", [options.GenericName, "C"], parent, (parent as TypeDecl)?.Base);
            var op1 = new TypeDecl("Op", [options.GenericName], parent, (parent as TypeDecl)?.Base);

            if (!options.Switch.HasFlag(TemplateVariants.IComparable))
            {
                var CBW = new ComparableRewriter(options.CompareName); // IComparer<T> => IComparable<T>

                cb = (MethodDeclarationSyntax)CBW.Visit(decl);
                yield return GeneratedMethod.Create(template, cb, cmp1);
                if (iw is not null)
                {
                    ci = (MethodDeclarationSyntax)CBW.Visit(iw);
                    yield return GeneratedMethod.Create(template, ci, cmp1);
                }
            }

            if (!options.Switch.HasFlag(TemplateVariants.TComparer))
            {
                var CGW = ComparerGenericRewriter.Instance; // Comparison<T> => TComparer

                yield return GeneratedMethod.Create(template, (MethodDeclarationSyntax)CGW.Visit(decl), cmp2);
                if (iw is not null)
                    yield return GeneratedMethod.Create(template, (MethodDeclarationSyntax)CGW.Visit(iw), cmp2);
            }

            if (!options.Switch.HasFlag(TemplateVariants.IComparisonOperators))
            {
                CSharpSyntaxRewriter LTW = capabilities.UsesLessThan
                    ? new LessThanRewriter(options.CompareName) // compare => operator <
                    : new ComparableRewriter(options.CompareName); // fallback: IComparer<T> => IComparable<T> with specialized Less()

                lt = (MethodDeclarationSyntax)LTW.Visit(decl);
                yield return GeneratedMethod.Create(template, lt, op1) with { Usings = [.. template.Usings, "using System.Numerics;"] };
                if (iw is not null)
                {
                    li = (MethodDeclarationSyntax)LTW.Visit(iw);
                    yield return GeneratedMethod.Create(template, li, op1);
                }
            }

            if (!options.IsPublic)
                yield break;

            var wrapper = template.Name.StartsWith("Sort")
                ? $"{template.OriginType.Containing?.Name ?? template.OriginType.Name}{template.Name}"
                : template.Name;

            if (cb is not null)
            {
                foreach (var target in Enumerable.Where([cb, ci], d => d is not null))
                {
                    var method = target!.WithIdentifier(F.Identifier(wrapper));
                    yield return GeneratedMethod.CreateWrapper(template, ToContract(method), _idp);
                    yield return GeneratedMethod.CreateWrapper(
                        template,
                        ToWrapper(method, cmp1, template.Name, false),
                        _csp);

                    if (lt is null)
                        yield return GeneratedMethod.CreateWrapper(template,
                            ToWrapper(method, cmp1, template.Name, false),
                            _ndp);
                    if (!options.IsUnstable)
                        yield return GeneratedMethod.CreateWrapper(
                            template,
                            ToWrapper(method, cmp1, template.Name, false),
                            _fdp);
                }
            }

            if (lt is not null)
            {
                foreach (var target in Enumerable.Where([lt, li], d => d is not null))
                {
                    var method = target!.WithIdentifier(F.Identifier(wrapper));
                    yield return GeneratedMethod.CreateWrapper(template,
                        ToWrapper(method, op1, template.Name, false),
                        _ndp);

                    if (options.IsUnstable)
                        yield return GeneratedMethod.CreateWrapper(template,
                            ToWrapper(method, op1, template.Name, template.Name.StartsWith("Sort")),
                            _fdp);
                }
            }

            if (!options.Switch.HasFlag(TemplateVariants.TComparer))
            {
                var CWW = new ComparerWrapperRewriter(options.CompareName); // strip Comparer for wrappers
                foreach (var target in Enumerable.Where([decl, iw], d => d is not null))
                {
                    var method = target!.WithIdentifier(F.Identifier(wrapper));
                    yield return GeneratedMethod.CreateWrapper(template,
                        (MethodDeclarationSyntax)CWW.Visit(ToWrapper(method, cmp2, template.Name, false)),
                        _dsp);
                }
            }
        }
    }

    private sealed record GeneratedFile(
        string FilePath,
        SourceText Source);

    private static IEnumerable<GeneratedFile> RenderFiles(ImmutableArray<GeneratedMethod> variants, Capabilities capabilities)
    {
        return variants
            .GroupBy(v => v.FilePath)
            .Select(f =>
            {
                try
                {
                    var usings = f
                        .SelectMany(static v => v.Usings)
                        .Select(static s => s.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static s => s, StringComparer.Ordinal)
                        .Select(static s => s switch
                        {
                            _ when s.StartsWith("using static") 
                                => F.UsingDirective(F.ParseName(s[13..].TrimEnd(';')))
                                    .WithStaticKeyword(F.Token(SyntaxKind.StaticKeyword)),
                            _ when s.StartsWith("using")
                                => F.UsingDirective(F.ParseName(s[6..].TrimEnd(';'))),
                            _ => null!,
                        })
                        .Where(u => u is not null)
                        .ToArray();
                    usings[0] = usings[0].WithLeadingTrivia(F.Trivia(F.NullableDirectiveTrivia(F.Token(SyntaxKind.EnableKeyword), true)));

                    var ns = f.Select(static v => v.Namespace).Distinct().Single();
                    var name = f.First().TypeName;

                    TypeDeclarationSyntax decl = name.Kind switch
                    {
                        TypeKind.Interface => F.InterfaceDeclaration(name.Name),
                        _ => F.ClassDeclaration(name.Name),
                    };
                    decl = decl.WithModifiers([F.Token(SyntaxKind.PartialKeyword)])
                        .WithMembers([.. f.Select(static v => v.Declaration)]);
                    if (name.TypeParameters.Any())
                    {
                        decl = decl
                            .WithTypeParameterList(F.TypeParameterList([.. name.TypeParameters.Select(F.TypeParameter)]))
                            .WithMembers([.. decl.Members]);
                        decl = name switch
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
                                        capabilities.UsesLessThan
                                            ? F.TypeConstraint(F.ParseTypeName($"global::System.Numerics.IComparisonOperators<{T}, {T}, bool>"))
                                            : F.TypeConstraint(F.ParseTypeName($"global::System.IComparable<{T}>"))
                                    ])
                            ]),
                            _ => decl
                        };
                    }
                    if (name.Base is TypeDeclCore b)
                    {
                        decl = decl.WithBaseList(F.BaseList([F.SimpleBaseType(b.Syntax)]));
                    }
                    // add modifiers for inner-most nested classes
                    if (name.Containing is not null)
                    {
                        decl = name.Base is null
                            ? decl.WithModifiers([F.Token(SyntaxKind.InternalKeyword), F.Token(SyntaxKind.AbstractKeyword), F.Token(SyntaxKind.PartialKeyword)])
                            : decl.WithModifiers([F.Token(SyntaxKind.InternalKeyword), F.Token(SyntaxKind.NewKeyword), F.Token(SyntaxKind.SealedKeyword), F.Token(SyntaxKind.PartialKeyword)]);
                    }

                    TypeDeclCore? c = name.Containing;
                    while (c is not null)
                    {
                        decl = F.ClassDeclaration(c.Name)
                            .WithModifiers([F.Token(SyntaxKind.PartialKeyword)])
                            .WithMembers([decl]);
                        c = c.Containing;
                    }

                    var root = F.CompilationUnit()
                        .WithUsings([.. usings])
                        .WithMembers([F.FileScopedNamespaceDeclaration(F.ParseName(ns))
                            .WithMembers([decl])]);

                    return new GeneratedFile(
                        f.Key,
                        root.NormalizeWhitespace().GetText(Encoding.UTF8));
                }
                catch
                {
                    // TODO: provide a diagnostic for this error
                    return null!;
                }                
            })
            .Where(f => f is not null);
    }

    private sealed class BehaviorMapComparer
        : IEqualityComparer<IDictionary<string, CallSiteBehaviors>>
    {
        public static readonly BehaviorMapComparer Instance = new();

        public bool Equals(IDictionary<string, CallSiteBehaviors> x, IDictionary<string, CallSiteBehaviors> y)
        {
            if (ReferenceEquals(x, y)) return true;
            else if (x is null || y is null) return x is null && y is null;
            else if (x.Count != y.Count) return false;

            using var X = x.GetEnumerator();
            using var Y = y.GetEnumerator();

            while (X.MoveNext() && Y.MoveNext())
            {
                if (X.Current.Key != Y.Current.Key || X.Current.Value != Y.Current.Value)
                    return false;
            }

            return true;
        }

        public int GetHashCode(IDictionary<string, CallSiteBehaviors> obj)
        {
            int hash = 0;

            foreach (var kv in obj)
            {
                hash ^= kv.Key.GetHashCode();
                hash ^= kv.Value.GetHashCode();
            }

            return hash;
        }
    }
}
