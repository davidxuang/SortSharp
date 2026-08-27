using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

[Generator]
internal sealed partial class OverloadGenerator : IIncrementalGenerator
{
    private record struct Capabilities(bool ComparisonOperators);

    private static readonly CSharpSyntaxRewriter trimmer = new TrimmingRewriter();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var templates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{typeof(OverloadTemplateAttribute).Namespace}.{nameof(OverloadTemplateAttribute)}",
                static (node, _) => node is MemberDeclarationSyntax,
                static (ctx, ct) => AnalyzeTemplates(ctx, ct));

        var capabilities = context.CompilationProvider
            .Select(static (cmpl, _) =>
                new Capabilities(
                    ComparisonOperators: cmpl.GetTypeByMetadataName(
                        "System.Numerics.IComparisonOperators`3") is not null));

        var behaviors = templates
            .Select(static (tmpl, _) => (tmpl.Name, tmpl.Behavior))
            .Where(static pair => pair.Behavior != CallSiteBehaviors.None)
            .Collect()
            .Select(static (pairs, _) => BuildBehaviors(pairs))
            .WithComparer(BehaviorMapComparer.Instance);

        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{typeof(SortSpecializationAttribute).Namespace}.{nameof(SortSpecializationAttribute)}",
                static (node, _) => node is MemberDeclarationSyntax,
                static (ctx, ct) => (ResolveType((ITypeSymbol)ctx.TargetSymbol), GetArgument<string>(ctx.Attributes.Single(), 0)))
            .Collect();

        context.RegisterSourceOutput(
            templates
                .Combine(behaviors.Combine(capabilities.Combine(targets)))
                .SelectMany(static (tuple, ct) =>
                {
                    var template = tuple.Left;
                    var behaviors = tuple.Right.Left;
                    var capabilities = tuple.Right.Right.Left;
                    var targets = tuple.Right.Right.Right;
                    return template switch
                    {
                        TemplateMethod<BasicOptions> b => GenerateMethods(b, targets, behaviors, ct).ToImmutableArray(),
                        TemplateMethod<ComparisonOptions> c => GenerateMethods(c, capabilities, behaviors, ct).ToImmutableArray(),
                        _ => []
                    };
                })
                .Collect()
                .Combine(capabilities)
                .Select(static (tuple, _) =>
                    RenderFiles(tuple.Left, (decl, meta) => TransformSyntax(decl, meta, tuple.Right))),
            static (spc, files) =>
            {
                foreach (var file in files)
                    spc.AddSource(file.FilePath, file.Source);
            });
    }

    internal record class TypeDeclCore(
        string Namespace,
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
    internal sealed record class TypeDecl(
        string Namespace,
        string Name,
        ImmutableArray<string> TypeParameters,
        TypeDeclCore? Containing = null,
        TypeDeclCore? Base = null,
        TypeKind Kind = TypeKind.Class,
        SyntaxKind Modifier = default) : TypeDeclCore(Namespace, Name, TypeParameters, Containing);

    internal static TypeDecl ResolveType(ITypeSymbol symbol)
        => new(symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            symbol is INamedTypeSymbol named
                ? [.. named.TypeParameters.Select(p => p.Name)]
                : [],
            symbol.ContainingType is not null
                ? ResolveType(symbol.ContainingType)
                : null,
            symbol.BaseType is not null && symbol.BaseType.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType)
                ? ResolveType(symbol.BaseType)
                : null,
            symbol.IsValueType ? TypeKind.Struct : TypeKind.Class,
            symbol switch
            {
                { IsAbstract: true, IsSealed: true } => SyntaxKind.StaticKeyword,
                { IsAbstract: true } => SyntaxKind.AbstractKeyword,
                { IsSealed: true } => SyntaxKind.SealedKeyword,
                _ => default,
            });

    private static T? GetArgument<T>(AttributeData? attr, int index)
    {
        if (attr == null) return default;
        if (index < attr.ConstructorArguments.Length && attr.ConstructorArguments[index].Value is object obj)
            try { return (T)obj; } catch { }
        return default;
    }

    private static ImmutableArray<T> GetArgumentArray<T>(AttributeData? attr, int index)
    {
        if (attr == null) return [];
        if (index < attr.ConstructorArguments.Length)
            try { return attr.ConstructorArguments[index].Values.Select(a => a.Value).Cast<T>().ToImmutableArray(); } catch { }
        return [];
    }

    private static T? GetNamedArgument<T>(AttributeData? attr, string name)
    {
        if (attr == null) return default;
        if (attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is object obj)
            try { return (T)obj; } catch { }
        return default;
    }

    internal abstract record TemplateMethod(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<string> Usings,
        string OriginFolder,
        string OriginNamespace,
        TypeDecl OriginType,
        string Name,
        Accessibility Accessibility,
        CallSiteBehaviors Behavior);

    internal sealed record TemplateMethod<T>(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<string> Usings,
        string OriginFolder,
        string OriginNamespace,
        TypeDecl OriginType,
        string Name,
        Accessibility Accessibility,
        CallSiteBehaviors Behavior,
        T? Options)
        : TemplateMethod(Declaration, Usings, OriginFolder, OriginNamespace, OriginType, Name, Accessibility, Behavior)
        where T : notnull;

    private static TemplateMethod AnalyzeTemplates(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var decl = (MethodDeclarationSyntax)ctx.TargetNode;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(decl, ct) ?? throw new InvalidOperationException();
        var attr = ctx.Attributes.Single();

        var usings = decl.SyntaxTree.GetCompilationUnitRoot(ct).Usings.Select(u => u.WithoutTrivia().ToFullString());
        var ns = symbol.ContainingNamespace.ToDisplayString();
        var parent = ResolveType(symbol.ContainingType);

        IEnumerable<string> segments = decl.SyntaxTree.FilePath.Split('\\', '/');
        segments = segments.Reverse().Skip(1).TakeWhile(s => !s.StartsWith("SortSharp")).Reverse();
        var relative = string.Join("/", segments).Trim().Trim('/');

        var itemType = GetArgument<string>(attr, 0);
        var comparer = GetArgument<string>(attr, 1);
        var itemIds = GetArgumentArray<string>(attr, 2);
        var isSpan = symbol.Parameters
            .Where(p => itemIds.Contains(p.Name))
            .Any(p => p.Type.Name == nameof(Span<>) || p.Type.Name == nameof(ReadOnlySpan<>));

        var valid = attr.ConstructorArguments.Reverse().SkipWhile(a => a is { IsNull: true } or { Kind: TypedConstantKind.Array, Values: [] }).Reverse().Count();
        var behavior = attr.ConstructorArguments[..valid] switch
        {
            { Length: 1 } when symbol.Parameters.Any(p => p is
                { Type: ITypeParameterSymbol, RefKind: RefKind.Ref } or // ref T
                { Type: INamedTypeSymbol { Name: nameof(Span<>), TypeArguments: [ITypeParameterSymbol] } }) // Span<T>
                => CallSiteBehaviors.RepeatCall,
            { Length: 3 } => itemIds.Select(a => symbol.Parameters.IndexOf(symbol.Parameters.First(p => p.Name == a)))
                .Select(i => (uint)CallSiteBehaviors.RepeatArgument0 << i)
                .Cast<CallSiteBehaviors>()
                .Aggregate(CallSiteBehaviors.None, (a, b) => a | b),
            _ => CallSiteBehaviors.None,
        };

        var sortClass = symbol.ContainingType;
        while (sortClass.ContainingType is not null)
            sortClass = sortClass.ContainingType;
        var sortAttr = sortClass.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == nameof(SortAttribute));

        var disable = GetNamedArgument<DefaultOverloads>(sortAttr, nameof(SortAttribute.Disable))
            | GetNamedArgument<DefaultOverloads>(attr, nameof(OverloadTemplateAttribute.Disable));
        var enable = GetNamedArgument<OptionalOverloads>(attr, nameof(OverloadTemplateAttribute.Enable));
        var properties = GetNamedArgument<SortProperties>(sortAttr, nameof(SortAttribute.Properties));

        if (sortAttr is not null && properties.HasFlag(SortProperties.NonComparison))
            return new TemplateMethod<BasicOptions>(decl, [.. usings], relative, ns, parent, symbol.Name, symbol.DeclaredAccessibility, behavior,
                new(itemType!, itemIds, isSpan, disable, enable));
        else if(string.IsNullOrEmpty(comparer) && itemIds.Length == 0)
            return new TemplateMethod<object>(decl, [.. usings], relative, ns, parent, symbol.Name, symbol.DeclaredAccessibility, behavior, null);
        else
            return new TemplateMethod<ComparisonOptions>(decl, [.. usings], relative, ns, parent, symbol.Name, symbol.DeclaredAccessibility, behavior,
                new(itemType!, comparer!, itemIds, isSpan, properties, disable, enable));
    }

    private static IDictionary<string, CallSiteBehaviors> BuildBehaviors(ImmutableArray<(string Name, CallSiteBehaviors Behavior)> pairs)
    {
        // presets
        SortedDictionary<string, CallSiteBehaviors> dict = new()
        {
            ["Less"] = CallSiteBehaviors.None,
            ["Compare"] = CallSiteBehaviors.None,
            ["CopyTo"] = CallSiteBehaviors.RepeatCall,
            ["CopyToFill"] = CallSiteBehaviors.RepeatCall,
            ["Dispose"] = CallSiteBehaviors.RepeatCall,
            ["Reverse"] = CallSiteBehaviors.RepeatCall,
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

    internal sealed record GeneratedMethod(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<string> Usings,
        string FilePath,
        string Namespace,
        TypeDecl TypeName)
    {
        internal static GeneratedMethod Create<T>(
            TemplateMethod<T> template,
            MemberDeclarationSyntax declaration,
            TypeDecl? child = null)
            where T : notnull
        {
            var type = child ?? template.OriginType;
            IEnumerable<string> segments = [template.OriginFolder, $"{type.HintName}.g"];
            return new GeneratedMethod(
                (MethodDeclarationSyntax)trimmer.Visit(declaration),
                template.Usings,
                Path.Combine([.. segments.Where(s => !string.IsNullOrEmpty(s))]),
                template.OriginNamespace,
                type);
        }

        internal static GeneratedMethod CreateWrapper<T>(
            TemplateMethod<T> template,
            MemberDeclarationSyntax declaration,
            TypeDecl type)
            where T : notnull
        {
            return new GeneratedMethod(
                (MethodDeclarationSyntax)trimmer.Visit(declaration),
                template.Usings,
                $"{type.HintName}.g",
                type.Namespace,
                type);
        }
    }

    internal sealed record GeneratedFile(
        string FilePath,
        SourceText Source);

    internal static IEnumerable<GeneratedFile> RenderFiles(ImmutableArray<GeneratedMethod> variants, Func<TypeDeclarationSyntax, TypeDecl, TypeDeclarationSyntax> callback)
    {
        return variants
            .GroupBy(v => v.FilePath)
            .Select(f =>
            {
                try
                {
                    var usings = string.Join(
                        "\r\n",
                        f.SelectMany(static v => v.Usings)
                            .Select(static s => s.Trim())
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(static s => s, StringComparer.Ordinal));
                    var root = F.ParseCompilationUnit($"""
                        // <auto-generated/>
                        #nullable enable
                        {usings}
                        """);

                    var ns = f.Select(static v => v.Namespace).Distinct().Single();
                    var meta = f.First().TypeName;

                    TypeDeclarationSyntax decl = meta.Kind switch
                    {
                        TypeKind.Interface => F.InterfaceDeclaration(meta.Name),
                        TypeKind.Struct => F.StructDeclaration(meta.Name),
                        _ => F.ClassDeclaration(meta.Name),
                    };
                    decl = decl.WithModifiers([F.Token(SyntaxKind.PartialKeyword)])
                        .WithMembers([.. f.Select(static v => v.Declaration)]);
                    if (meta.TypeParameters.Any())
                    {
                        decl = decl
                            .WithTypeParameterList(F.TypeParameterList([.. meta.TypeParameters.Select(F.TypeParameter)]))
                            .WithMembers([.. decl.Members]);
                        decl = callback(decl, meta);
                    }
                    if (meta.Base is TypeDeclCore b)
                    {
                        decl = decl.WithBaseList(F.BaseList([F.SimpleBaseType(b.Syntax)]));
                    }
                    // add modifiers for inner-most nested classes
                    if (meta.Containing is not null)
                    {
                        var modifier = f.Select(n => n.TypeName.Modifier).FirstOrDefault(m => m != default);
                        decl = meta.Modifier != default
                            ? decl.WithModifiers([F.Token(SyntaxKind.InternalKeyword), F.Token(modifier), F.Token(SyntaxKind.PartialKeyword)])
                            : decl.WithModifiers([F.Token(SyntaxKind.InternalKeyword), F.Token(SyntaxKind.PartialKeyword)]);
                    }

                    TypeDeclCore? c = meta.Containing;
                    while (c is not null)
                    {
                        decl = F.ClassDeclaration(c.Name)
                            .WithModifiers([F.Token(SyntaxKind.PartialKeyword)])
                            .WithMembers([decl]);
                        c = c.Containing;
                    }

                    root = root.WithMembers([F.FileScopedNamespaceDeclaration(F.ParseName(ns))
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
