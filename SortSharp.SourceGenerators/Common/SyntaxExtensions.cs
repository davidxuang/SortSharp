using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.SourceGenerators.Impl;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Common;

internal static class SyntaxExtensions
{
    extension(SyntaxNode node)
    {
        public bool HasSimpleName(string? name)
        {
            return name is not null && node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any(n => n.Identifier.Text == name);
        }
    }

    extension<T>(IEnumerable<T> nodes) where T : SyntaxNode
    {
        public T[] Squeeze()
        {
            var arr = nodes.ToArray();
            if (arr.Length == 2)
            {
                var a = arr[0];
                var b = arr[1];

                if (a.HasTrailingTrivia && b.HasLeadingTrivia)
                {
                    var x = a.GetTrailingTrivia().ToFullString();
                    var y = b.GetLeadingTrivia().ToFullString();

                    if (x.Contains("\n") && y.Contains("\n"))
                    {
                        return [a.WithoutTrailingTrivia(), b];
                    }
                }
            }
            return arr;
        }
    }

    extension(TypeSyntax type)
    {
        public bool TestType(string? name)
            => (type is IdentifierNameSyntax { Identifier.Text: var i } && i == name)
            || (type is PredefinedTypeSyntax { Keyword.Text: var p } && p == name)
            || (type is RefTypeSyntax { Type: TypeSyntax r } && TestType(r, name))
            || (type is GenericNameSyntax { TypeArgumentList.Arguments: [TypeSyntax g] } && TestType(g, name))
            || (type is NullableTypeSyntax { ElementType: GenericNameSyntax { TypeArgumentList.Arguments: [TypeSyntax n] } } && TestType(n, name));

        public bool TestDataType(string? name)
            => (type is IdentifierNameSyntax { Identifier.Text: var i } && i == name)
            || (type is PredefinedTypeSyntax { Keyword.Text: var p } && p == name)
            || (type is GenericNameSyntax { Identifier.Text: nameof(Span<>) or nameof(ReadOnlySpan<>), TypeArgumentList.Arguments: [TypeSyntax g] } && TestDataType(g, name));
    }

    extension(InvocationExpressionSyntax inv)
    {
        public string GetMethodName() => inv.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax gen => gen.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            MemberBindingExpressionSyntax mb => mb.Name.Identifier.Text,
            _ => throw new InvalidOperationException($"Unsupported invocation expression: {inv}")
        };

        public int GetContentHashCode()
        {
            var values = MemoryMarshal.Cast<byte, ulong>(inv.GetText().GetContentHash().AsSpan());
            var hash = new HashCode();
            for (int i = 0; i < values.Length; i++)
                hash.Add(values[i]);
            return hash.ToHashCode();
        }
    }

    extension(IEnumerable<StatementSyntax> statements)
    {
        public SyntaxNode SingleWithBlock()
        {
            return statements.Count() > 1
                ? F.Block(statements.Squeeze())
                : statements.Single();
        }
    }

    extension(IEnumerable<InvocationInfo> infos)
    {
        public InvocationInfo? Match(InvocationExpressionSyntax inv)
        {
            var hash = inv.GetContentHashCode();
            var target = inv.GetMethodName();
            var dep = infos.Where(dep => dep.ContentHash == hash && dep.Name == target)
                .Distinct()
                .SingleOrDefault();
            if (dep is not null)
                return dep;
            var candidates = infos.Where(dep => dep.Name == target)
                .Distinct(new InvocationInfo.EffectComparer());
            return candidates.SingleOrDefault();
        }

        public bool ShouldRepeatCall(InvocationExpressionSyntax inv)
            => infos.Match(inv)?.Expansion.IsT1 ?? false;

        public bool ShouldMirrorArguments(InvocationExpressionSyntax inv, out MirrorArguments arguments)
        {
            var match = infos.Match(inv);
            var should = match?.Expansion?.IsT2 == true;
            arguments = should ? match!.Expansion.AsT2 : default;
            return should;
        }
    }
}
