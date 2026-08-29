using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.SourceGenerators.Api;
using SortSharp.SourceGenerators.Common;

namespace SortSharp.SourceGenerators;

[Generator]
internal sealed class ApiOverloadGenerator : IIncrementalGenerator
{
    private record struct Capabilities(bool IKeySelector, bool Half, bool NInt);

    private static readonly CSharpSyntaxRewriter trimmer = new TrimmingRewriter();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var capabilities = context.CompilationProvider
            .Select(static (cmpl, _) =>
                new Capabilities(
                    IKeySelector: cmpl.GetTypeByMetadataName(typeof(IKeySelector<,>).FullName)?.DeclaredAccessibility == Accessibility.Public,
                    Half: cmpl.GetTypeByMetadataName("System.Half") is not null,
                    NInt: cmpl.GetTypeByMetadataName("System.IntPtr")?.AllInterfaces.Any(i => i.ImplementsInterface(typeof(IComparable<>))) == true));

        context.RegisterSourceOutput(
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    $"{typeof(ImplTemplateAttribute).Namespace}.{nameof(ApiTemplateAttribute)}",
                    static (node, _) => node is MethodDeclarationSyntax,
                    static (ctx, ct) => ctx)
                .Combine(capabilities)
                .SelectMany(static (tuple, _) =>
                {
                    var (ctx, capabilities) = tuple;
                    return GenerateMethods(ctx, capabilities);
                })
                .Collect()
                .Select(static (methods, _) =>
                    GeneratedMethod.Render(methods, static (decl, meta) => decl)),
            static (spc, files) =>
            {
                foreach (var file in files)
                    spc.AddSource(file.FilePath, file.Source);
            });
    }

    static readonly HashSet<string> _int = ["sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong"];
    static readonly HashSet<string> _float = ["float", "double"];

    static IEnumerable<GeneratedMethod> GenerateMethods(GeneratorAttributeSyntaxContext ctx, Capabilities capabilities)
    {
        var decl = (MethodDeclarationSyntax)ctx.TargetNode;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(decl)!;
        var type = TypeDef.Resolve(symbol.ContainingType);

        IEnumerable<string> segments = decl.SyntaxTree.FilePath.Split('\\', '/');
        segments = segments.Reverse().Skip(1).TakeWhile(s => !s.StartsWith("SortSharp")).Reverse();
        var relative = string.Join("/", segments).Trim().Trim('/');
        segments = [relative, $"{type.HintName}.g"];

        var attr = ctx.Attributes[0];
        var source = (string)attr.ConstructorArguments[0].Value!;
        bool isSourceGeneric = symbol.TypeParameters.Any(p => p.Name == source);
        bool keySelector = attr.GetNamedArgument(nameof(ApiTemplateAttribute.KeySelector), false);
        var calls = new ApiCallInfo(decl, ctx.SemanticModel);

        foreach (var target in targets(capabilities, decl, source))
        {
            var rewritter = new TypeRewriter(source, target);
            yield return Create(rewritter.Visit(decl));
        }

        string?[] variants = isSourceGeneric ? [null] : [source, .. targets(capabilities, decl, source)];
        if (calls.HasFrom)
        {
            if (capabilities.IKeySelector)
                foreach (string? target in variants)
                {
                    SyntaxNode? variant = new KeySelectorRewriter(
                        calls, source, isSourceGeneric, composed: true).Visit(decl);
                    if (target is not null && target != source)
                        variant = new TypeRewriter(source, target).Visit(variant);
                    yield return Create(variant);
                }
        }
        else
        {
            foreach (string? target in variants)
            {
                SyntaxNode? variant = new KeyValueRewriter(calls, source, isSourceGeneric).Visit(decl);
                if (target is not null && target != source)
                    variant = new TypeRewriter(source, target).Visit(variant);
                yield return Create(variant);
            }

            if (keySelector && capabilities.IKeySelector)
                foreach (string? target in variants)
                {
                    SyntaxNode? variant = new KeySelectorRewriter(calls, source, isSourceGeneric, composed: false).Visit(decl);
                    if (target is not null && target != source)
                        variant = new TypeRewriter(source, target).Visit(variant);
                    yield return Create(variant);
                }
        }

        GeneratedMethod Create(SyntaxNode? declaration) => new(
            (MethodDeclarationSyntax)(trimmer.Visit(declaration)
                ?? throw new InvalidOperationException()),
            [.. decl.SyntaxTree.GetCompilationUnitRoot().Usings.Select(u => u.WithoutTrivia().ToFullString())],
            Path.Combine([.. segments.Where(s => !string.IsNullOrEmpty(s))]),
            symbol.ContainingNamespace.ToDisplayString(),
            type);

        static IEnumerable<string> targets(Capabilities capabilities, MethodDeclarationSyntax decl, string name)
        {
            if (_int.Contains(name))
            {
                foreach (var i in _int)
                {
                    if (i != name) yield return i;
                }
                //if (capabilities.NInt)
                //{
                //    yield return "nint";
                //    yield return "nuint";
                //}
            }
            else if (_float.Contains(name))
            {
                foreach (var f in _float)
                {
                    if (f != name) yield return f;
                }
                if (capabilities.Half)
                {
                    yield return "Half";
                }
            }
        }
    }
}
