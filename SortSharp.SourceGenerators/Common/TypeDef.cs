using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Common;

internal record class TypeDefCore(
        string Namespace,
        string Name,
        ImmutableArray<string> TypeParameters,
        TypeDefCore? Containing = null)
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

    public virtual bool Equals(TypeDefCore? other) => other is not null
        && Namespace == other.Namespace
        && Name == other.Name
        && TypeParameters.Length == other.TypeParameters.Length
        && (Containing?.Equals(other.Containing) ?? other.Containing is null);
    public override int GetHashCode() => HashCode.Combine(Namespace, Name, TypeParameters.Length, Containing);
}

internal sealed record class TypeDef(
    string Namespace,
    string Name,
    ImmutableArray<string> TypeParameters,
    TypeDefCore? Containing = null,
    TypeDefCore? Base = null,
    TypeKind Kind = TypeKind.Class,
    SyntaxKind Modifier = default) : TypeDefCore(Namespace, Name, TypeParameters, Containing)
{
    internal static TypeDef Resolve(ITypeSymbol symbol)
        => new(symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            symbol is INamedTypeSymbol named
                ? [.. named.TypeParameters.Select(p => p.Name)]
                : [],
            symbol.ContainingType is not null
                ? Resolve(symbol.ContainingType)
                : null,
            symbol.BaseType is not null && symbol.BaseType.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType)
                ? Resolve(symbol.BaseType)
                : null,
            symbol.IsValueType ? TypeKind.Struct : TypeKind.Class,
            symbol switch
            {
                { IsAbstract: true, IsSealed: true } => SyntaxKind.StaticKeyword,
                { IsAbstract: true } => SyntaxKind.AbstractKeyword,
                { IsSealed: true } => SyntaxKind.SealedKeyword,
                _ => default,
            });

    public bool Equals(TypeDef? other)
        => other is not null
            && Namespace == other.Namespace
            && Name == other.Name
            && TypeParameters.Length == other.TypeParameters.Length
            && (Containing?.Equals(other.Containing) ?? other.Containing is null)
            && (Base?.Equals(other.Base) ?? other.Base is null)
            && Kind == other.Kind
            && Modifier == other.Modifier;
    public override int GetHashCode()
        => HashCode.Combine(Namespace, Name, TypeParameters.Length, Containing, Base, Kind, Modifier);
}
