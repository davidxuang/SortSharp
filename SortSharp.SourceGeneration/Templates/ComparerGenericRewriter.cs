using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGeneration.Templates;

internal sealed class ComparerGenericRewriter : CSharpSyntaxRewriter
{
    internal static ComparerGenericRewriter Instance { get; } = new();

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        return node.Identifier.Text == nameof(Comparison<>)
            ? F.IdentifierName("C").WithTriviaFrom(node)
            : base.VisitGenericName(node);
    }
}

internal sealed class ComparerWrapperRewriter(string name) : CSharpSyntaxRewriter
{
    private static readonly SyntaxToken _type = F.Identifier(nameof(IComparer<>));

    //public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    //{
    //    return base.VisitGenericName(node.Identifier.Text == nameof(Comparison<>)
    //        ? node.WithIdentifier(_type.WithTriviaFrom(node.Identifier))
    //        : node);
    //}

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        return node.Identifier.Text == "C"
            ? F.GenericName(_type, F.TypeArgumentList([F.IdentifierName("T")]))
            : base.VisitIdentifierName(node);
    }

    public override SyntaxNode? VisitParameterList(ParameterListSyntax node)
    {
        node = base.VisitParameterList(node) as ParameterListSyntax ?? throw new InvalidOperationException();
        return node.WithParameters([.. node.Parameters.Where(p => p.Identifier.Text != name)]);
    }

    public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node)
    {
        node = base.VisitArgumentList(node) as ArgumentListSyntax ?? throw new InvalidOperationException();
        return node.WithArguments([
            .. node.Arguments.Select(a => a.Expression is IdentifierNameSyntax { Identifier.Text: var s } && s == name
                ? F.Argument(F.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    F.GenericName(F.Identifier(nameof(Comparer<>)), F.TypeArgumentList([F.IdentifierName("T")])),
                    F.IdentifierName(nameof(Comparer<>.Default))))
                : a)]);
    }
}
