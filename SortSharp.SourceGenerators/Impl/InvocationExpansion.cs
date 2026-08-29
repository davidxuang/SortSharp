using Microsoft.CodeAnalysis;
using OneOf;
using OneOf.Types;
using SortSharp.SourceGenerators.Common;

namespace SortSharp.SourceGenerators.Impl;

internal sealed class InvocationExpansion(OneOf<None, RepeatCall, MirrorArguments> _)
    : OneOfBase<None, RepeatCall, MirrorArguments>(_), IEquatable<InvocationExpansion>
{
    public bool Equals(InvocationExpansion other) => Match(
        _ => other.IsT0,
        _ => other.IsT1,
        a => other.IsT2 && a.Equals(other.AsT2));
    public override int GetHashCode() => Match(_ => int.MinValue, _ => int.MaxValue, a => a.GetHashCode());

    internal static InvocationExpansion None { get; } = new(new None());
    internal static InvocationExpansion Whole { get; } = new(new RepeatCall());
    internal static InvocationExpansion Arguments(IEnumerable<int> indices) => new(new MirrorArguments(indices));

    internal static OneOf<Exception, InvocationExpansion> Resolve(IMethodSymbol method)
    {
        try
        {
            var attr = method.GetAttributeOf<ImplTemplateAttribute>();
            // explictly handle some non-Pure BCL methods.
            if (attr is null)
            {
                var typeName = method.ContainingType.FullName;
                if (typeName == typeof(Span<>).FullName || typeName == typeof(ReadOnlySpan<>).FullName)
                    return method.Name switch
                    {
                        nameof(Span<>.CopyTo) => Whole,
                        _ => None,
                    };
                else if (typeName == typeof(MemoryExtensions).FullName)
                    return method.Name switch
                    {
                        nameof(MemoryExtensions.Reverse) => Whole,
                        _ => None,
                    };
                else if (method.ContainingType.ImplementsInterface(typeof(IDisposable))
                    && method.Name is nameof(IDisposable.Dispose))
                    return Whole;
                else if (method.ContainingSymbol.GetAttributeOf<GenerateInlineArrayAttribute>() is not null
                    && method.Name == InlineArrayGenerator.CopyMethodName)
                    return Whole;
                else
                    return None;
            }
            else
            {
                var itemType = attr.GetArgument<string>(0);
                var itemNames = attr.GetArgumentArray<string>(1);
                var kv = attr.GetNamedArgument<OverloadOption>(nameof(ImplTemplateAttribute.KeyValue));
                return itemNames switch
                {
                    _ when kv == OverloadOption.Specialized => None,
                    { Length: > 0 } => Arguments(itemNames
                        .Select(name => method.Parameters.Single(p => p.Name == name))
                        .Select(param => method.Parameters.IndexOf(param))),
                    _ when method.Parameters.Any(p =>
                        (p is { Type: var t0, RefKind: RefKind.Ref } && t0.Name == itemType) ||
                        (p is { Type: INamedTypeSymbol { Name: nameof(Span<>), TypeArguments: [var t1] } } && t1.Name == itemType))
                        => Whole,
                    _ => None
                };
            }
        }
        catch (Exception e)
        {
            return e;
        }
    }
}

internal readonly record struct RepeatCall();
internal readonly struct MirrorArguments : IEquatable<MirrorArguments>
{
    readonly ulong _bits;

    public MirrorArguments(IEnumerable<int> indices)
    {
        foreach (int index in indices)
        {
            if (index < 0 || index >= sizeof(ulong) * 8)
                throw new ArgumentException("Value overflows.", nameof(indices));
            _bits |= (1ul << index);
        }
    }

    public readonly bool Test(int index)
    {
        if (index < 0 || index >= sizeof(ulong) * 8)
            throw new ArgumentOutOfRangeException(nameof(index));
        return (_bits & (1ul << index)) != 0;
    }

    public bool Equals(MirrorArguments other) => _bits == other._bits;
    public override int GetHashCode() => _bits.GetHashCode();
}
