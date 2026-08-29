using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Common;

internal sealed class StackallocRewriter(string name) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        return node.Update(
            VisitList(node.AttributeLists),
            VisitToken(node.OpenBraceToken),
            F.List(node.Statements.SelectMany(stmt => stmt switch
            {
                LocalDeclarationStatementSyntax l => TransformLocalDeclarationStatement(l).Squeeze(),
                _ => [(StatementSyntax)Visit(stmt)]
            })),
            VisitToken(node.CloseBraceToken));
    }

    private IEnumerable<StatementSyntax> TransformLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        node = base.VisitLocalDeclarationStatement(node) as LocalDeclarationStatementSyntax ?? throw new InvalidOperationException();
        if (node.Declaration.Type.TestType(name) && node.Declaration.Variables.Count == 1 && node.Declaration.Variables.Single().Initializer is { Value: StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax { RankSpecifiers: [{ Sizes: [var size] }] } } })
        {
            var spanName = node.Declaration.Variables.Single().Identifier.Text;
            var ownerName = spanName.EndsWith("v") ? "owner_v" : "owner";
            yield return F.ParseStatement($"using var {ownerName} = global::System.Buffers.MemoryPool<{name}>.Shared.Rent({size.ToFullString()});");
            yield return F.ParseStatement($"Span<{name}> {spanName} = {ownerName}.Memory.Span.Sub(0, {size.ToFullString()});");
        }
        else
            yield return node;
    }
}
