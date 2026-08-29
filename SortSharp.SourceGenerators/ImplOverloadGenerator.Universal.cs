using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OneOf;
using SortSharp.SourceGenerators.Common;
using SortSharp.SourceGenerators.Impl;
using SortSharp.SourceGenerators.Impl.Universal;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

partial class ImplOverloadGenerator
{
    private sealed record BroadcastReceiver(
        TypeDef Containing,
        ImmutableArray<string> Channels,
        string? DataType,
        bool IsDataUnmanaged)
    {
        internal static OneOf<Diagnostic, BroadcastReceiver> Resolve(GeneratorAttributeSyntaxContext ctx)
        {

            var attr = ctx.Attributes.Single();
            var channels = attr.GetArgumentArray<string>(0);
            var typeName = attr.GetNamedArgument<string?>(nameof(ReceiverAttribute.Type));
            var parent = TypeDef.Resolve((ITypeSymbol)ctx.TargetSymbol);
            BroadcastReceiver? receiver = new(parent, channels, typeName, false);
            if (typeName is not null)
            {
                var syntax = F.ParseTypeName(typeName);
                TypeInfo typeInfo = ctx.SemanticModel.GetSpeculativeTypeInfo(
                    ctx.TargetNode.SpanStart,
                    syntax,
                    SpeculativeBindingOption.BindAsTypeOrNamespace);
                if (typeInfo.Type is ITypeSymbol symbol)
                    return receiver with { IsDataUnmanaged = symbol.IsUnmanagedType };
                else
                    return Diagnostic.Create(DiagnosticDescriptors.UnkownType, ctx.TargetNode.GetLocation(), typeName);
            }
            return receiver;
        }

        public bool Equals(BroadcastReceiver? other)
            => other is not null
                && Containing.Equals(other.Containing)
                && Channels.SequenceEqual(other.Channels)
                && DataType == other.DataType
                && IsDataUnmanaged == other.IsDataUnmanaged;
        public override int GetHashCode()
            => HashCode.Combine(
                Containing,
                HashCode.CombineAll(Channels),
                DataType,
                IsDataUnmanaged);
    }

    private sealed record UniversalTemplate(
        MethodDeclarationSyntax Declaration,
        ImmutableArray<InvocationInfo> Invocations,
        TemplateInfo Info,
        ImmutableArray<BroadcastReceiver> Receivers)
        : Template(Declaration, Invocations, Info)
    {
        public static UniversalTemplate Create(Template template, IEnumerable<BroadcastReceiver> receivers, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return new UniversalTemplate(
                template.Declaration,
                template.Invocations,
                template.Info,
                template.Info.Options?.Broadcast is string b
                    ? [.. receivers.Where(r => r.Channels.Contains(b))]
                    : []);
        }

        public IEnumerable<GeneratedMethod> Generate()
        {
            var options = Info.Options!;
            List<(MethodDeclarationSyntax, TypeDef?)> variants = [(Declaration, null)];

            if (options.ItemNames.Any() && options.KeyValue == OverloadOption.Enable)
            {
                KeyValueRewriter KV = options.AreItemsSpan // <?> => <T, V>
                    ? KeyValueRewriter.CreateForSpans(Invocations, options.ItemType, options.ItemNames)
                    : KeyValueRewriter.CreateForItems(Invocations, options.ItemType, options.ItemNames);
                var kv = (MethodDeclarationSyntax)KV.Visit(Declaration);
                variants.Add((kv, null));
                yield return Variant(kv);
            }

            if (options.ItemType is not null && options.KeySelector)
            {
                KeySelectorRewriter KS = new(Invocations, options.ItemType!); // <.., V, TSelector>
                var ks = (MethodDeclarationSyntax)KS.Visit(Declaration)!;
                var child = Declaration.TypeParameterList?.Parameters.Any(p => p.Identifier.Text == options.ItemType) == true
                    ? new TypeDef(Info.Namespace, "From", ["V", "T", "TSelector"], Info.ContainingType, Modifier: Info.ContainingType.Modifier)
                    : new TypeDef(Info.Namespace, "From", ["V", "TSelector"], Info.ContainingType, Modifier: Info.ContainingType.Modifier);
                variants.Add((ks, child));
                yield return Variant(ks, child);
            }

            if (options.Broadcast is null)
                yield break;

            foreach (var receiver in Receivers)
            {
                if (receiver.DataType is null || string.IsNullOrEmpty(receiver.DataType))
                {
                    foreach (var (v, child) in variants)
                        yield return Variant(v, child is not null ? child with { Containing = receiver?.Containing } : receiver?.Containing);
                }
                else
                {
                    CSharpSyntaxRewriter TR = new TypeRewriter("T", receiver.DataType);
                    if (!receiver.IsDataUnmanaged)
                        TR = new ComposedRewriter(TR, new StackallocRewriter(receiver.DataType));
                    foreach (var (v, child) in variants)
                    {
                        yield return Variant((MethodDeclarationSyntax)TR.Visit(v), child is not null ? child with { Containing = receiver?.Containing } : receiver?.Containing);
                    }
                }
            }
        }
    }
}
