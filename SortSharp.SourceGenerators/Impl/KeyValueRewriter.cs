using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

internal class ItemsRewriter(
    IDictionary<string, CallSiteBehaviors> behaviors,
    string typeName,
    IEnumerable<(string, string)> k,
    IEnumerable<(string, string)> v) : CSharpSyntaxRewriter
{
    private readonly IdentifierRewriter _K = new(null, k);
    private readonly IdentifierRewriter _V = new(new TypeParameterRewriter(typeName, "V"), v);
    private readonly HashSet<string> _RO = new();

    private static (string, string) Mangling(string name)
        => (name, name.Length == 1 ? $"{name}v" : $"{name}_v");

    public static ItemsRewriter CreateForItems(
        IDictionary<string, CallSiteBehaviors> behaviors,
        string generic,
        IEnumerable<string> names)
        => new(behaviors, generic, [], names.Select(Mangling));

    public static ItemsRewriter CreateForSpans(
        IDictionary<string, CallSiteBehaviors> behaviors,
        string generic,
        IEnumerable<string> names)
        => new(behaviors, generic, [(names.First(), "keys")], 
            [("keys", "items"), ..names.Skip(1).Select(Mangling)]);

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        node = _K.Visit(node) as MethodDeclarationSyntax ?? throw new InvalidOperationException();
        foreach (var p in node.ParameterList.Parameters)
        {
            if (!_V.Map.ContainsKey(p.Identifier.Text) && p.Type.TestParamType(typeName))
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
        return node
            .WithParameterList(node.ParameterList.WithParameters(F.SeparatedList(
                node.ParameterList.Parameters.SelectMany(p => p switch
                {
                    _ when _V.Map.ContainsKey(p.Identifier.Text) =>
                    [
                        p,
                        _V.Visit(p) as ParameterSyntax ?? throw new InvalidOperationException()
                    ],
                    _ => new[] { p },
                }))));
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
        if (!_V.Map.Keys.Any(node.HasIdentifierName) || _RO.Any(node.HasIdentifierName))
            return false; // does not contain should-shadow variables, or contains reference-only variables (e.g. pivot)
        else if (node is InvocationExpressionSyntax ie && TryGetBehavior(ie.Expression, out var b) && b == CallSiteBehaviors.RepeatCall)
            return true;
        else if (node is ConditionalAccessExpressionSyntax { WhenNotNull: ExpressionSyntax wnn })
            return ShouldRepeat(wnn);
        else if (node is AssignmentExpressionSyntax ae)
        {
            if (_V.Map.Values.Any(ae.HasIdentifierName))
                return false; // contains already-shadowed variables (?)
            else if (_V.Map.Keys.Any(ae.Left.HasIdentifierName) && _V.Map.Keys.Any(ae.Right.HasIdentifierName))
                return true; // contains should-shadow variables on both sides
            else if (ae.Left is IdentifierNameSyntax { Identifier.Text: var left } && _V.Map.ContainsKey(left))
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
        node = base.VisitExpressionStatement(node) as ExpressionStatementSyntax ?? throw new InvalidOperationException();
        if (ShouldRepeat(node.Expression))
        {
            yield return node.WithTrailingTrivia(F.ElasticCarriageReturnLineFeed);
            yield return _V.Visit(node.WithoutLeadingTrivia()) as ExpressionStatementSyntax ?? throw new InvalidOperationException();
        }
        else
            yield return node;
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
            _V.Map[name] = name_v;
            yield return node.WithTrailingTrivia(F.ElasticCarriageReturnLineFeed);
            if (node.Declaration.Variables.Count == 1 && node.Declaration.Variables.Single().Initializer is { Value: StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax { RankSpecifiers: [{ Sizes: [var size] }] } } })
            {
                yield return F.ParseStatement($"using var owner_v = global::System.Buffers.MemoryPool<V>.Shared.Rent({size.ToFullString()});");
                yield return F.ParseStatement($"Span<V> {name_v} = owner_v.Memory.Span.Sub(0, {size.ToFullString()});");
            }
            else
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
            _V.Map[name] = name_v;
            yield return node.WithTrailingTrivia(F.ElasticCarriageReturnLineFeed);
            yield return _V.Visit(node.WithoutLeadingTrivia()) as UsingStatementSyntax ?? throw new InvalidOperationException();
        }
        else
            yield return node;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        node = base.VisitInvocationExpression(node) as InvocationExpressionSyntax ?? throw new InvalidOperationException();

        if (TryGetBehavior(node.Expression, out var b))
        {
            node = node
                .WithArgumentList(F.ArgumentList(F.SeparatedList(
                    node.ArgumentList.Arguments
                        .SelectMany((arg, a) =>
                        {
                            if (b.HasFlag((CallSiteBehaviors)((uint)CallSiteBehaviors.RepeatArgument0 << a)))
                            {
                                if (arg.Expression is DeclarationExpressionSyntax de && de.Type.TestType(typeName)
                                    && de.Designation is SingleVariableDesignationSyntax svd)
                                {
                                    var name = svd.Identifier.Text;
                                    var name_v = name.Length == 1 ? $"{name}v" : $"{name}_v";
                                    _V.Map[name] = name_v;
                                }
                                return [ arg, _V.Visit(arg) as ArgumentSyntax ?? throw new InvalidOperationException() ];
                            }
                            else
                                return new[] { arg };
                        }))))
                .WithTriviaFrom(node);
        }

        return node;
    }

    private bool TryGetBehavior(ExpressionSyntax node, out CallSiteBehaviors behavior, string suffix = "")
    {
        if (node is IdentifierNameSyntax id)
        {
            return behaviors.TryGetValue($"{id.Identifier.Text}{suffix}", out behavior);
        }
        else if (node is MemberBindingExpressionSyntax mb)
        {
            return behaviors.TryGetValue($"{mb.Name.Identifier.Text}{suffix}", out behavior);
        }
        else if (node is MemberAccessExpressionSyntax ma)
        {
            var name = ma.Name.Identifier.Text;
            return behaviors.TryGetValue(name, out behavior)
                || TryGetBehavior(ma.Expression, out behavior, $".{name}{suffix}");
        }
        behavior = default;
        return false;
    }
}

internal sealed class TypeParameterRewriter(string old, string name) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitPredefinedType(PredefinedTypeSyntax node)
        => node.Keyword.Text == old
            ? F.IdentifierName(F.Identifier(name)).WithTriviaFrom(node)
            : base.VisitPredefinedType(node);

    public override SyntaxNode? VisitTypeParameter(TypeParameterSyntax node)
        => base.VisitTypeParameter(node.Identifier.Text == old
            ? node.WithIdentifier(F.Identifier(name)).WithTriviaFrom(node)
            : node);

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        => base.VisitIdentifierName(node.Identifier.Text == old
            ? node.WithIdentifier(F.Identifier(name)).WithTriviaFrom(node)
            : node);
}

internal sealed class IdentifierRewriter(CSharpSyntaxRewriter? pre, params IEnumerable<(string, string)> map) : CSharpSyntaxRewriter
{
    internal readonly Dictionary<string, string> Map = map.ToDictionary(t => t.Item1, t => t.Item2);

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        return base.Visit(pre is not null ? pre.Visit(node) : node);
    }

    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        return base.VisitParameter(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n).WithTriviaFrom(node.Identifier))
            : node);
    }

    public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        return base.VisitVariableDeclarator(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n).WithTriviaFrom(node.Identifier))
            : node);
    }

    public override SyntaxNode? VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
    {
        return base.VisitSingleVariableDesignation(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n).WithTriviaFrom(node.Identifier))
            : node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        return base.VisitIdentifierName(Map.TryGetValue(node.Identifier.Text, out var n)
            ? node.WithIdentifier(F.Identifier(n)).WithTriviaFrom(node)
            : node);
    }
}
