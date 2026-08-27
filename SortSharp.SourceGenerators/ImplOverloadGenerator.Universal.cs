using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

partial class OverloadGenerator
{
    private sealed record BasicOptions(
        string ItemType,
        ImmutableArray<string> ItemNames,
        bool IsItemsSpan,
        DefaultOverloads Disable,
        OptionalOverloads Enable);

    private static IEnumerable<GeneratedMethod> GenerateMethods(
        TemplateMethod<BasicOptions> template,
        IEnumerable<(TypeDecl, string)> targets,
        IDictionary<string, CallSiteBehaviors> behaviors,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var decl = template.Declaration;
        var options = template.Options;

        ImmutableArray<MethodDeclarationSyntax> D = [decl];
        if (options!.ItemNames.Any() && !options.Disable.HasFlag(DefaultOverloads.KeyValue))
        {
            ItemsRewriter IW = options!.IsItemsSpan // <?> => <T, V>
                ? ItemsRewriter.CreateForSpans(behaviors, options.ItemType, options.ItemNames)
                : ItemsRewriter.CreateForItems(behaviors, options.ItemType, options.ItemNames);
            var iw = (MethodDeclarationSyntax)IW.Visit(decl);
            D = D.Add(iw);
            yield return GeneratedMethod.Create(template, iw);
        }

        if (!options.Enable.HasFlag(OptionalOverloads.SiblingSpecializations))
            yield break;

        foreach (var (target, typeName) in targets.Where(t => t.Item1.Containing?.HintName == template.OriginType.Containing!.HintName))
        {
            if (string.IsNullOrEmpty(typeName))
                foreach (var d in D)
                    yield return GeneratedMethod.Create(template, d, target);
            else
            {
                var IR = new SpecializationRewriter("T", typeName);
                foreach (var d in D)
                {
                    var ir = (MethodDeclarationSyntax)IR.Visit(d);
                    yield return GeneratedMethod.Create(template, ir, target);
                }
            }
        }
    }
}

internal sealed class SpecializationRewriter(string old, string name) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitPredefinedType(PredefinedTypeSyntax node)
        => node.Keyword.Text == old
            ? F.ParseTypeName(name).WithTriviaFrom(node)
            : base.VisitPredefinedType(node);

    public override SyntaxNode? VisitTypeParameter(TypeParameterSyntax node)
        => node.Identifier.Text == old
            ? F.ParseTypeName(name).WithTriviaFrom(node)
            : base.VisitTypeParameter(node);

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        => node.Identifier.Text == old
            ? F.ParseTypeName(name).WithTriviaFrom(node)
            : base.VisitIdentifierName(node);

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
            yield return F.ParseStatement($"using var owner = global::System.Buffers.MemoryPool<{name}>.Shared.Rent({size.ToFullString()});");
            yield return F.ParseStatement($"Span<{name}> {node.Declaration.Variables.Single().Identifier.Text} = owner.Memory.Span.Sub(0, {size.ToFullString()});");
        }
        else
            yield return node;
    }
}
