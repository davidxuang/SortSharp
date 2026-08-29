using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SortSharp.SourceGenerators.Impl;

namespace SortSharp.SourceGenerators.Common;

internal static class SymbolExtensions
{

    extension(ISymbol symbol)
    {
        public AttributeData? GetAttributeOf<T>()
            where T : Attribute
            => symbol.GetAttributes()
                .SingleOrDefault(attr => attr.AttributeClass?.FullName == typeof(T).FullName);
    }

    extension(ITypeSymbol type)
    {
        public string FullName => type switch
        {
            { ContainingType: null, ContainingNamespace.IsGlobalNamespace: true } => type.MetadataName,
            { ContainingType: null } => $"{type.ContainingNamespace.ToDisplayString()}.{type.MetadataName}",
            _ => $"{type.ContainingType.FullName}+{type.MetadataName}"
        };
        public bool ImplementsInterface(Type other) => type
            .AllInterfaces
            .Any(iface => iface.FullName == other.FullName);
    }

    extension(INamedTypeSymbol type)
    {
        public INamedTypeSymbol TopType => type.ContainingType is not null
            ? type.ContainingType.TopType
            : type;
        public IEnumerable<INamedTypeSymbol> SelfAndNestedTypes()
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers())
            {
                foreach (var descendant in SelfAndNestedTypes(nested))
                    yield return descendant;
            }
        }
        public IEnumerable<INamedTypeSymbol> NestedTypesAndSelf()
        {
            foreach (var nested in type.GetTypeMembers())
            {
                foreach (var descendant in NestedTypesAndSelf(nested))
                    yield return descendant;
            }
            yield return type;
        }
    }

    extension(AttributeData? attr)
    {
        internal T? GetArgument<T>(int index)
        {
            if (attr == null) return default;
            if (index < attr.ConstructorArguments.Length && attr.ConstructorArguments[index].Value is object obj)
                try { return (T)obj; } catch { }
            return default;
        }

        internal ImmutableArray<T> GetArgumentArray<T>(int index)
        {
            if (attr == null) return [];
            if (index < attr.ConstructorArguments.Length)
                try { return [.. attr.ConstructorArguments[index].Values.Select(a => a.Value).Cast<T>()]; } catch { }
            return [];
        }

        internal T? GetNamedArgument<T>(string name, T? defaultValue = default)
        {
            if (attr == null) return defaultValue;
            if (attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is object obj)
                try { return (T)obj; } catch { }
            return defaultValue;
        }
    }
}