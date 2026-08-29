using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OneOf;
using SortSharp.SourceGenerators.Common;
using SortSharp.SourceGenerators.Impl;

namespace SortSharp.SourceGenerators;

[Generator]
internal sealed partial class ImplOverloadGenerator : IIncrementalGenerator
{
    private record struct Capabilities(bool ComparisonOperators);

    private static readonly CSharpSyntaxRewriter trimmer = new TrimmingRewriter();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var capabilities = context.CompilationProvider
            .Select(static (cmpl, _) =>
                new Capabilities(
                    ComparisonOperators: cmpl.GetTypeByMetadataName(
                        "System.Numerics.IComparisonOperators`3") is not null));

        var receivers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{typeof(ReceiverAttribute).Namespace}.{nameof(ReceiverAttribute)}",
                static (node, _) => node is MemberDeclarationSyntax,
                static (node, _) => BroadcastReceiver.Resolve(node))
            .Collect();

        context.RegisterSourceOutput(
            receivers.Select((receivers, _) => receivers.Lefts()),
            static (spc, diags) => diags.Iterate(spc.ReportDiagnostic));

        var outputs = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                typeof(ImplTemplateAttribute).FullName,
                static (node, _) => node is MethodDeclarationSyntax,
                Template.Resolve)
            .Combine(capabilities.Combine(receivers.Select((receivers, _) => receivers.Rights())))
            .SelectMany(static (tuple, ct) =>
            {
                var (template, (capabilities, receivers)) = tuple;
                return template
                    .MapT1(tmpl => tmpl.Info?.Options switch
                    {
                        { ComparerName: not null } or { SortProps: SortProperties.Comparison }
                            => [ComparisonTemplate.Create(tmpl, capabilities)],
                        { ItemNames.Length: > 0 } or { Broadcast: not null }
                            => [UniversalTemplate.Create(tmpl, receivers, ct)],
                        _ => Enumerable.Empty<Template>(),
                    })
                    .Bisequence();
            })
            .WithComparer(new Template.Comparer())
            .SelectMany(static (tmpl, _) => tmpl.MapT1(
                r => r switch
                {
                    ComparisonTemplate c => c.Generate(),
                    UniversalTemplate u => u.Generate(),
                    _ => []
                })
                .Sequence())
            .Collect()
            .Combine(capabilities)
            .Select(static (tuple, _) =>
            {
                var (tmpl, capabilities) = tuple;
                var (Lefts, Rights) = tmpl.Seperate();
                return (Lefts, GeneratedMethod.Render(Rights,
                    (decl, meta) => TransformType(decl, meta, capabilities)));
            });

        context.RegisterSourceOutput(
            outputs,
            static (spc, outputs) =>
            {
                var (diags, files) = outputs;
                diags.Iterate(spc.ReportDiagnostic);
                files.Iterate(file => spc.AddSource(file.FilePath, file.Source));
            });
    }
    
    internal record Template(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<InvocationInfo> Invocations,
        TemplateInfo Info)
    {
        internal static OneOf<IEnumerable<Diagnostic>, Template> Resolve(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            var decl = (MethodDeclarationSyntax)ctx.TargetNode;
            var symbol = (IMethodSymbol)ctx.TargetSymbol;
            var info = TemplateInfo.Resolve(decl, symbol, ct);
            // skip expensive analysis for tagging-only templates
            return info.Options is null 
                ? OneOf<IEnumerable<Diagnostic>, Template>.FromT0([])
                : InvocationInfo.Resolve(ctx, decl, ct)
                    .ValidateAll()
                    .MapT1(r => new Template(decl, [..r], info));
        }

        internal GeneratedMethod Variant(
            MethodDeclarationSyntax decl,
            TypeDef? child = null)
        {
            var type = child ?? Info.ContainingType;
            IEnumerable<string> segments = [Info.RelativeSourcePath, $"{type.HintName}.g"];
            return new(
                (MethodDeclarationSyntax)trimmer.Visit(decl),
                Info.Usings,
                Path.Combine([.. segments.Where(s => !string.IsNullOrEmpty(s))]),
                Info.Namespace,
                type);
        }

        internal GeneratedMethod Wrapper(
            MemberDeclarationSyntax decl,
            TypeDef type)
        {
            return new GeneratedMethod(
                (MethodDeclarationSyntax)trimmer.Visit(decl),
                Info.Usings,
                $"{type.HintName}.g",
                type.Namespace,
                type);
        }

        public virtual bool Equals(Template? other)
            => other is not null
                && Declaration.IsEquivalentTo(other.Declaration)
                && Invocations.SequenceEqual(other.Invocations)
                && Info.Equals(other.Info);
        public override int GetHashCode()
            => HashCode.Combine(
                HashCode.CombineAll(Invocations),
                Info);

        internal sealed class Comparer : IEqualityComparer<OneOf<Diagnostic, Template>>
        {
            public bool Equals(OneOf<Diagnostic, Template> x, OneOf<Diagnostic, Template> y)
                => x.Match(_ => false, t1 => y.Match(_ => false, t2 => t1?.Equals(t2) ?? false));
            public int GetHashCode(OneOf<Diagnostic, Template> obj)
                => obj.Match(_ => 0, t => t?.GetHashCode() ?? 0);
        }
    }

    internal sealed record TemplateInfo(
        ImmutableArray<string> Usings,
        string RelativeSourcePath,
        string Namespace,
        TypeDef ContainingType,
        string Name,
        Accessibility Accessibility,
        TemplateOptions? Options)
    {
        internal static TemplateInfo Resolve(MethodDeclarationSyntax decl, IMethodSymbol symbol, CancellationToken ct)
        {
            var usings = decl.SyntaxTree.GetCompilationUnitRoot(ct).Usings.Select(static u => u.WithoutTrivia().ToFullString());
            var relative = decl.SyntaxTree.FilePath
                .Split('\\', '/').AsEnumerable()
                .Reverse().Skip(1).TakeWhile(static s => !s.StartsWith(nameof(SortSharp)))
                .Reverse().Join("/").Trim().Trim('\\', '/');
            var ns = symbol.ContainingNamespace.ToDisplayString();
            var type = TypeDef.Resolve(symbol.ContainingType);
            var name = symbol.Name;
            var access = symbol.DeclaredAccessibility;
            return new([..usings], relative, ns, type, name, access, TemplateOptions.Resolve(symbol));
        }

        public bool Equals(TemplateInfo? other)
            => other is not null
                && Usings.SequenceEqual(other.Usings)
                && RelativeSourcePath == other.RelativeSourcePath
                && Namespace == other.Namespace
                && ContainingType.Equals(other.ContainingType)
                && Name == other.Name
                && Accessibility == other.Accessibility
                && (Options?.Equals(other.Options) ?? other.Options is null);
        public override int GetHashCode()
            => HashCode.Combine(
                HashCode.CombineAll(Usings),
                RelativeSourcePath,
                Namespace,
                ContainingType,
                Name,
                Accessibility,
                Options);
    }

    internal sealed record TemplateOptions(
        string ItemType,
        ImmutableArray<string> ItemNames,
        bool AreItemsSpan,
        OverloadOption KeyValue,
        bool KeySelector,
        string? Broadcast = null,
        string? ComparerName = null,
        ComparerOverloads Disable = default,
        //OptionalOverloads Enable = default,
        SortProperties? SortProps = null)
    {
        internal static TemplateOptions? Resolve(IMethodSymbol method)
        {
            var attr = method.GetAttributeOf<ImplTemplateAttribute>();
            var itemType = attr.GetArgument<string>(0);
            var itemNames = attr.GetArgumentArray<string>(1);

            var isSpan = method.Parameters
                .Where(p => itemNames.Contains(p.Name))
                .Any(p => p.Type.Name == nameof(Span<>) || p.Type.Name == nameof(ReadOnlySpan<>));

            var keyValue = attr.GetNamedArgument<OverloadOption>(nameof(ImplTemplateAttribute.KeyValue),
                defaultValue: OverloadOption.Enable);

            var sort = method.ContainingType.TopType;
            var sortAttr = sort.GetAttributeOf<SortAttribute>();
            var sortProps = sortAttr?.GetNamedArgument<SortProperties>(nameof(SortAttribute.Properties));

            var keySelector = attr.GetNamedArgument<bool>(nameof(ImplTemplateAttribute.KeySelector),
                defaultValue: sortAttr is not null && sortProps?.HasFlag(SortProperties.Comparison) == false);
            var broadcast = attr.GetNamedArgument<string>(nameof(ImplTemplateAttribute.Broadcast));

            var comparer = attr.GetNamedArgument<string>(nameof(ImplTemplateAttribute.Comparer));
            var disable = sortAttr.GetNamedArgument<ComparerOverloads>(nameof(SortAttribute.Disable))
                | attr.GetNamedArgument<ComparerOverloads>(nameof(ImplTemplateAttribute.Disable));
            //var enable = attr.GetNamedArgument<OptionalOverloads>(nameof(ImplTemplateAttribute.Enable));

            if (!string.IsNullOrEmpty(comparer) || itemNames.Any() || !string.IsNullOrEmpty(broadcast))
            {
                if (string.IsNullOrEmpty(itemType))
                    throw new InvalidOperationException($"The '{nameof(ImplTemplateAttribute)}' attribute must specify a non-empty item type.");
                return new(itemType!, itemNames, isSpan, keyValue, keySelector, broadcast, comparer, disable, /* enable, */ sortProps);
            }
            else
                return null;
        }

        public bool Equals(TemplateOptions? other)
            => other is not null
                && ItemType == other.ItemType
                && ItemNames.SequenceEqual(other.ItemNames)
                && AreItemsSpan == other.AreItemsSpan
                && KeyValue == other.KeyValue
                && KeySelector == other.KeySelector
                && Broadcast == other.Broadcast
                && ComparerName == other.ComparerName
                && Disable == other.Disable
                //&& Enable == other.Enable
                && SortProps == other.SortProps;
        public override int GetHashCode()
            => HashCode.Combine(
                ItemType,
                HashCode.CombineAll(ItemNames),
                AreItemsSpan,
                KeyValue,
                KeySelector,
                Broadcast,
                HashCode.Combine(ComparerName, Disable, /* Enable, */ SortProps));
    }
}
