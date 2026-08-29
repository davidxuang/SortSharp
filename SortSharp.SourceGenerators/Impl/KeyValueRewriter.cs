using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SortSharp.SourceGenerators.Common;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators.Impl;

internal sealed class KeyValueRewriter : CSharpSyntaxRewriter
{
    private readonly IEnumerable<InvocationInfo> infos;
    private readonly string typeName;
    private readonly IdentifierRewriter _K;
    private readonly IdentifierRewriter _VI;
    private readonly ComposedRewriter _V;
    private static readonly StackallocRewriter _S = new("V");
    private readonly HashSet<string> _RO = [];

    public KeyValueRewriter(
        IEnumerable<InvocationInfo> infos,
        string typeName,
        IEnumerable<(string, string)> k,
        IEnumerable<(string, string)> v)
    {
        this.infos = infos;
        this.typeName = typeName;
        _K = new(k);
        _VI = new(v);
        _V = new(
            new TypeRewriter(typeName, "V", true),
            _VI);
    }

    private static (string, string) Mangling(string name)
        => (name, name.Length == 1 ? $"{name}v" : $"{name}_v");

    public static KeyValueRewriter CreateForItems(
        IEnumerable<InvocationInfo> infos,
        string generic,
        IEnumerable<string> names)
        => new(infos, generic, [], names.Select(Mangling));

    public static KeyValueRewriter CreateForSpans(
        IEnumerable<InvocationInfo> infos,
        string generic,
        IEnumerable<string> names)
        => new(infos, generic, [(names.First(), "keys")],
            [(names.First(), "items"), ..names.Skip(1).Select(Mangling)]);

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        foreach (var p in node.ParameterList.Parameters)
        {
            if (!_VI.Map.ContainsKey(p.Identifier.Text) && p.Type?.TestDataType(typeName) == true)
                _RO.Add(p.Identifier.Text); // record the parameter name to avoid repeating it in the body
        }
        node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax ?? throw new InvalidOperationException();

        if (node.TypeParameterList?.Parameters.Any() == true)
        {
            node = node.WithTypeParameterList(node.TypeParameterList.WithParameters([
                .. node.TypeParameterList.Parameters.SelectMany(p =>
                    p.Identifier.Text == typeName
                        ? [ p, _V.Visit(p) as TypeParameterSyntax ?? throw new InvalidOperationException() ]
                        : new[] { p })]));
        }
        else
        {
            node = node.WithTypeParameterList(F.TypeParameterList([F.TypeParameter("V")]));
        }
        node = node
            .WithParameterList(node.ParameterList.WithParameters(F.SeparatedList(
                node.ParameterList.Parameters.SelectMany(p => p switch
                {
                    _ when _VI.Map.ContainsKey(p.Identifier.Text) =>
                    [
                        p,
                        _V.Visit(p) as ParameterSyntax ?? throw new InvalidOperationException()
                    ],
                    _ => new[] { p },
                }))));
        return _S.Visit(_K.Visit(node));
    }

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        return node.Identifier.Text is "Context" or "TypeTraits" && node.TypeArgumentList.Arguments is [ IdentifierNameSyntax id ] && id.Identifier.Text == typeName
            ? node.WithTypeArgumentList(F.TypeArgumentList([F.ParseTypeName(typeName), F.ParseTypeName("V")]))
            : base.VisitGenericName(node);
    }

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        return node.Update(
            VisitList(node.AttributeLists),
            VisitToken(node.OpenBraceToken),
            F.List(node.Statements.SelectMany(stmt => stmt switch
            {
                ExpressionStatementSyntax e => TransformExpressionStatement(e).Squeeze(),
                LocalDeclarationStatementSyntax l => TransformLocalDeclarationStatement(l).Squeeze(),
                UsingStatementSyntax u => TransformUsingStatementStatement(u).Squeeze(),
                _ => [(StatementSyntax)Visit(stmt)]
            })),
            VisitToken(node.CloseBraceToken));
    }

    public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        return node.Parent is BlockSyntax
            ? base.VisitExpressionStatement(node)
            : TransformExpressionStatement(node).SingleWithBlock().WithTriviaFrom(node);
    }

    private bool ShouldRepeat(ExpressionSyntax node)
    {
        if (!_VI.Map.Keys.Any(node.HasSimpleName) || _RO.Any(node.HasSimpleName))
            return false; // does not contain should-shadow variables, or contains reference-only variables (e.g. pivot)
        else if (node is InvocationExpressionSyntax inv && infos.ShouldRepeatCall(inv))
            return true;
        else if (node is ConditionalAccessExpressionSyntax { WhenNotNull: ExpressionSyntax wnn })
            return ShouldRepeat(wnn);
        else if (node is AssignmentExpressionSyntax asg)
        {
            if (_VI.Map.Values.Any(asg.HasSimpleName))
                return false; // contains already-shadowed variables (?)
            else if (_VI.Map.Keys.Any(asg.Left.HasSimpleName) && _VI.Map.Keys.Any(asg.Right.HasSimpleName))
                return true; // contains should-shadow variables on both sides
            else if (asg.Left is IdentifierNameSyntax { Identifier.Text: var left } && _VI.Map.ContainsKey(left))
                return true; // assigning to should-shadow variables
        }
        return false;
    }

    private IEnumerable<ExpressionSyntax> TryRepeatExpression(ExpressionSyntax node)
    {
        node = base.Visit(node) as ExpressionSyntax ?? throw new InvalidOperationException();
        if (ShouldRepeat(node))
        {
            yield return node.WithTrailingTrivia(F.ElasticCarriageReturnLineFeed);
            yield return _V.Visit(node.WithoutLeadingTrivia()) as ExpressionSyntax ?? throw new InvalidOperationException();
        }
        else
            yield return node;
    }

    private IEnumerable<StatementSyntax> TransformExpressionStatement(ExpressionStatementSyntax node)
    {
        if (ShouldRepeat(node.Expression))
        {
            node = base.VisitExpressionStatement(node) as ExpressionStatementSyntax ?? throw new InvalidOperationException();
            yield return node.WithTrailingTrivia(F.ElasticCarriageReturnLineFeed);
            yield return _V.Visit(node.WithoutLeadingTrivia()) as ExpressionStatementSyntax ?? throw new InvalidOperationException();
        }
        else
            yield return base.VisitExpressionStatement(node) as ExpressionStatementSyntax ?? throw new InvalidOperationException();
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        node = base.VisitForStatement(node) as ForStatementSyntax ?? throw new InvalidOperationException();
        return node.WithIncrementors(F.SeparatedList(node.Incrementors.SelectMany(e => TryRepeatExpression(e))));
    }

    public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        return node.Parent is BlockSyntax || !node.Declaration.Type.TestType(typeName)
            ? base.VisitLocalDeclarationStatement(node)
            : throw new InvalidOperationException();
    }

    private IEnumerable<StatementSyntax> TransformLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        node = base.VisitLocalDeclarationStatement(node) as LocalDeclarationStatementSyntax ?? throw new InvalidOperationException();
        if (node.Declaration.Type.TestType(typeName))
        {
            var name = node.Declaration.Variables.Single().Identifier.Text;
            var name_v = name.Length == 1 ? $"{name}v" : $"{name}_v";
            _VI.Map[name] = name_v;
            yield return node.WithTrailingTrivia(F.ElasticCarriageReturnLineFeed);
            yield return _V.Visit(node.WithoutLeadingTrivia()) as LocalDeclarationStatementSyntax ?? throw new InvalidOperationException();
        }
        else
            yield return node;
    }

    private IEnumerable<UsingStatementSyntax> TransformUsingStatementStatement(UsingStatementSyntax node)
    {
        node = base.VisitUsingStatement(node) as UsingStatementSyntax ?? throw new InvalidOperationException();
        if (node.Declaration?.Type?.TestType(typeName) == true)
        {
            var name = node.Declaration!.Variables.Single().Identifier.Text;
            var name_v = name.Length == 1 ? $"{name}v" : $"{name}_v";
            _VI.Map[name] = name_v;
            yield return node.WithTrailingTrivia(F.ElasticCarriageReturnLineFeed);
            yield return _V.Visit(node.WithoutLeadingTrivia()) as UsingStatementSyntax ?? throw new InvalidOperationException();
        }
        else
            yield return node;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var info = infos.Match(node);

        if (info is not null)
        {
            if (info.Expansion.IsT2)
            {
                var mirror = info.Expansion.AsT2;
                node = node
                    .WithArgumentList(F.ArgumentList(F.SeparatedList(
                        node.ArgumentList.Arguments
                            .SelectMany((arg, a) =>
                            {
                                if (mirror.Test(a))
                                {
                                    if (arg.Expression is DeclarationExpressionSyntax de && de.Type.TestType(typeName)
                                        && de.Designation is SingleVariableDesignationSyntax svd)
                                    {
                                        var name = svd.Identifier.Text;
                                        var name_v = name.Length == 1 ? $"{name}v" : $"{name}_v";
                                        _VI.Map[name] = name_v;
                                    }
                                    return [arg, _V.Visit(arg) as ArgumentSyntax ?? throw new InvalidOperationException()];
                                }
                                else
                                    return new[] { arg };
                            }))))
                    .WithTriviaFrom(node);
            }

            if (node.Expression is GenericNameSyntax gen && info.ExpandTypeArgument)
            {
                node = node.WithExpression(gen.WithTypeArgumentList(F.TypeArgumentList([.. gen.TypeArgumentList.Arguments, F.IdentifierName("V")])));
            }
        }

        return base.VisitInvocationExpression(node) as InvocationExpressionSyntax ?? throw new InvalidOperationException();
    }
}
