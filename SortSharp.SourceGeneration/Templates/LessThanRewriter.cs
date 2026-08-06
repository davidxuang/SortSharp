using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SortSharp.SourceGeneration.Templates;

internal sealed class LessThanRewriter(string name) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitParameterList(ParameterListSyntax node)
    {
        node = base.VisitParameterList(node) as ParameterListSyntax ?? throw new InvalidOperationException();
        return node.WithParameters([.. node.Parameters.Where(p => p.Identifier.Text != name)]);
    }

    public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node)
    {
        
        node = base.VisitArgumentList(node) as ArgumentListSyntax ?? throw new InvalidOperationException();
        return node.WithArguments([
            .. node.Arguments.Where(a => a.Expression is not IdentifierNameSyntax id || id.Identifier.Text != name)]);
    }
}
