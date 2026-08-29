using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Common;

internal sealed class TypeRewriter(string old, string name, bool isGeneric = false) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitPredefinedType(PredefinedTypeSyntax node)
        => node.Keyword.Text switch
        {
            var s when s != old => base.VisitPredefinedType(node),
            _ when isGeneric => F.IdentifierName(F.Identifier(name)).WithTriviaFrom(node),
            _ => F.ParseTypeName(name).WithTriviaFrom(node),
        };

    public override SyntaxNode? VisitTypeParameter(TypeParameterSyntax node)
        => node.Identifier.Text switch
        {
            var s when s != old => base.VisitTypeParameter(node),
            _ when isGeneric => node.WithIdentifier(F.Identifier(name)).WithTriviaFrom(node),
            _ => F.ParseTypeName(name).WithTriviaFrom(node),
        };

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        => node.Identifier.Text switch
        {
            var s when s != old => base.VisitIdentifierName(node),
            _ when isGeneric => node.WithIdentifier(F.Identifier(name)).WithTriviaFrom(node),
            _ => F.ParseTypeName(name).WithTriviaFrom(node),
        };
}
