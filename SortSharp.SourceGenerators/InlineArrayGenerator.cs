using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGenerators;

[Generator]
internal sealed class InlineArrayGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var create = context.CompilationProvider
            .Select(static (cmpl, _) => cmpl.GetTypeByMetadataName("System.Runtime.InteropServices.MemoryMarshal")
                ?.GetMembers("CreateSpan").OfType<IMethodSymbol>().Any() == true);

        var tuples = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{typeof(GenerateInlineArrayAttribute).Namespace}.{nameof(GenerateInlineArrayAttribute)}",
                static (node, _) => node is StructDeclarationSyntax,
                static (ctx, ct) =>
                {
                    var decl = (StructDeclarationSyntax)ctx.TargetNode;
                    var symbol = (ITypeSymbol)ctx.TargetSymbol;
                    var attr = ctx.Attributes.Single();
                    return (decl, symbol, attr);
                });

        var sources = tuples
            .Combine(create)
            .Select(static (tuple, _) =>
            {
                var ((decl, symbol, attr), create) = tuple;
                if (symbol.GetMembers().OfType<IFieldSymbol>().Any(s => !s.IsStatic))
                {
                    return Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "IA001",
                            "Inline array struct must have no instance fields",
                            "Inline array struct '{0}' must have no instance fields",
                            nameof(InlineArrayGenerator),
                            DiagnosticSeverity.Error,
                            true),
                        decl.Identifier.GetLocation(),
                        symbol.Name);
                }

                var type = (string)attr.ConstructorArguments[0].Value!;
                var length = (int)attr.ConstructorArguments[1].Value!;

                var node = decl.WithModifiers([F.Token(SyntaxKind.PartialKeyword)])
                    .WithAttributeLists([F.AttributeList([
                        F.Attribute(F.ParseName("global::System.Runtime.InteropServices.StructLayoutAttribute"))
                            .WithArgumentList(F.AttributeArgumentList([
                                F.AttributeArgument(F.ParseExpression("global::System.Runtime.InteropServices.LayoutKind.Sequential"))]))])])
                    .WithOpenBraceToken(F.Token(SyntaxKind.OpenBraceToken))
                    .WithMembers([
                        .. Enumerable.Range(0, length).Select(i => F.FieldDeclaration(
                            F.VariableDeclaration(F.IdentifierName(type))
                                .WithVariables([F.VariableDeclarator(F.Identifier($"item{i}"))]))),
                        GetRefMethod(type, "item0"),
                        GetCopyMethod(type, create
                            ? F.Block(
                                F.ParseStatement($"global::System.Diagnostics.Debug.Assert(span.Length <= {length});"),
                                F.ParseStatement($"global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(in item0, span.Length).CopyTo(span);"))
                            : F.Block(
                                F.ParseStatement($"global::System.Diagnostics.Debug.Assert(span.Length <= {length});"),
                                F.ParseStatement($"if (span.Length == 0) return;"),
                                F.ParseStatement("ref T src = ref item0;"),
                                F.ParseStatement("ref T dst = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);"),
                                F.ParseStatement("int i = 0;"),
                                F.ParseStatement("""
                                    for (; i < (span.Length & ~3); i += 4)
                                    {
                                        global::System.Runtime.CompilerServices.Unsafe.Add(ref dst, i) = global::System.Runtime.CompilerServices.Unsafe.Add(ref src, i);
                                        global::System.Runtime.CompilerServices.Unsafe.Add(ref dst, i + 1) = global::System.Runtime.CompilerServices.Unsafe.Add(ref src, i + 1);
                                        global::System.Runtime.CompilerServices.Unsafe.Add(ref dst, i + 2) = global::System.Runtime.CompilerServices.Unsafe.Add(ref src, i + 2);
                                        global::System.Runtime.CompilerServices.Unsafe.Add(ref dst, i + 3) = global::System.Runtime.CompilerServices.Unsafe.Add(ref src, i + 3);
                                    }
                                    """),
                                F.ParseStatement("""
                                    for (; i < span.Length; i++)
                                    {
                                        global::System.Runtime.CompilerServices.Unsafe.Add(ref dst, i) = global::System.Runtime.CompilerServices.Unsafe.Add(ref src, i);
                                    }
                                    """)
                                ))
                    ]).WithCloseBraceToken(F.Token(SyntaxKind.CloseBraceToken)).WithSemicolonToken(default);

                TypeDeclarationSyntax x = decl, y = node;
                List<string> names = [y.Identifier.Text];
                while (x.Parent is TypeDeclarationSyntax p)
                {
                    y = p.WithAttributeLists([])
                        .WithModifiers([F.Token(SyntaxKind.PartialKeyword)])
                        .WithBaseList(null)
                        .WithConstraintClauses([])
                        .WithOpenBraceToken(F.Token(SyntaxKind.OpenBraceToken))
                        .WithMembers([y])
                        .WithCloseBraceToken(F.Token(SyntaxKind.CloseBraceToken));
                    x = p;
                    names.Insert(0, x.Identifier.Text);
                }

                var root = F.CompilationUnit()
                    .WithUsings([F.UsingDirective(F.ParseName("SortSharp.Compat"))])
                    .WithMembers([F.FileScopedNamespaceDeclaration(F.ParseName(symbol.ContainingNamespace.ToDisplayString()))
                        .WithMembers([y])]);

                return (object)($"{string.Join(".", names)}.g.cs", root.NormalizeWhitespace().ToFullString());
            });

        context.RegisterSourceOutput(sources, static (spc, source) =>
        {
            if (source is ValueTuple<string, string> t)
                spc.AddSource(t.Item1, t.Item2);
            else if (source is Diagnostic d)
                spc.ReportDiagnostic(d);
            else
                throw new InvalidOperationException();
        });
    }

    private static MemberDeclarationSyntax GetRefMethod(string type, string element)
        => F.MethodDeclaration(F.RefType(F.ParseTypeName(type)), "Ref")
            .WithAttributeLists([F.AttributeList([
                F.Attribute(F.ParseName("global::System.Diagnostics.CodeAnalysis.UnscopedRefAttribute")),
                F.Attribute(F.ParseName("global::System.Runtime.CompilerServices.MethodImplAttribute"))
                    .WithArgumentList(F.AttributeArgumentList([
                        F.AttributeArgument(F.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"))]))])])
            .WithModifiers([F.Token(SyntaxKind.PublicKeyword)])
            .WithParameterList(F.ParameterList([
                F.Parameter(F.Identifier("i")).WithType(F.PredefinedType(F.Token(SyntaxKind.IntKeyword)))]))
            .WithExpressionBody(F.ArrowExpressionClause(
                F.ParseExpression($"ref global::System.Runtime.CompilerServices.Unsafe.Add(ref {element}, i)")))
            .WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken));

    private static MemberDeclarationSyntax GetCopyMethod(string type, BlockSyntax body)
        => F.MethodDeclaration(F.ParseTypeName("void"), "CopyToFill")
            .WithAttributeLists([F.AttributeList([
                F.Attribute(F.ParseName("global::System.Runtime.CompilerServices.MethodImplAttribute"))
                    .WithArgumentList(F.AttributeArgumentList([
                        F.AttributeArgument(F.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"))]))])])
            .WithModifiers([F.Token(SyntaxKind.PublicKeyword)])
            .WithParameterList(F.ParameterList([
                F.Parameter(F.Identifier("span")).WithType(F.ParseTypeName($"global::System.Span<{type}>"))]))
            .WithBody(body);
}
