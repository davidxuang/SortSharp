using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SortSharp.SourceGeneration;

[Generator]
internal sealed class SpecializationGenerator : IIncrementalGenerator
{
    private record struct Capabilities(bool Half, bool NInt);

    private static readonly CSharpSyntaxRewriter trimmer = new TrimmingRewriter();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var capabilities = context.CompilationProvider
            .Select(static (cmpl, _) =>
                new Capabilities(
                    Half: cmpl.GetTypeByMetadataName("System.Half") is not null,
                    NInt: cmpl.GetTypeByMetadataName("System.IntPtr")?.AllInterfaces.Any(i => i.MetadataName == "System.IComparable`1") == true));

        context.RegisterSourceOutput(
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    $"{typeof(OverloadTemplateAttribute).Namespace}.{nameof(SpecializationTemplateAttribute)}",
                    static (node, _) => node is MethodDeclarationSyntax,
                    static (ctx, ct) => ctx)
                .Combine(capabilities)
                .SelectMany(static (tuple, _) =>
                {
                    var (ctx, capabilities) = tuple;
                    return GetGeneratedMethods(ctx, capabilities);
                })
                .Collect()
                .Select(static (methods, _) =>
                    OverloadGenerator.RenderFiles(methods, (decl, meta) => decl)),
            static (spc, files) =>
            {
                foreach (var file in files)
                    spc.AddSource(file.FilePath, file.Source);
            });
    }

    static readonly HashSet<string> _int = ["sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong"];
    static readonly HashSet<string> _float = ["float", "double"];

    static IEnumerable<OverloadGenerator.GeneratedMethod> GetGeneratedMethods(GeneratorAttributeSyntaxContext ctx, Capabilities capabilities)
    {
        var decl = (MethodDeclarationSyntax)ctx.TargetNode;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(decl)!;
        var type = OverloadGenerator.ResolveType(symbol.ContainingType);

        IEnumerable<string> segments = decl.SyntaxTree.FilePath.Split('\\', '/');
        segments = segments.Reverse().Skip(1).TakeWhile(s => !s.StartsWith("SortSharp")).Reverse();
        var relative = string.Join("/", segments).Trim().Trim('/');
        segments = [relative, $"{type.HintName}.g"];

        var attr = ctx.Attributes[0];
        var source = (string)attr.ConstructorArguments[0].Value!;

        foreach (var target in targets(capabilities, decl, source))
        {
            var rewritter = new SpecializationRewriter(source, target);
            yield return new OverloadGenerator.GeneratedMethod(
                (MethodDeclarationSyntax)trimmer.Visit(rewritter.Visit(decl)),
                [.. decl.SyntaxTree.GetCompilationUnitRoot().Usings.Select(u => u.WithoutTrivia().ToFullString())],
                Path.Combine([.. segments.Where(s => !string.IsNullOrEmpty(s))]),
                symbol.ContainingNamespace.ToDisplayString(),
                type);
        }

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
