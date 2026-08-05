using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using F = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SortSharp.SourceGeneration;

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
                            "IA002",
                            "Inline array struct must have at least one static field",
                            "Inline array struct '{0}' must have at least one static field",
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
                                F.AttributeArgument(F.ParseExpression("global::System.Runtime.InteropServices.LayoutKind.Sequential")),
                                //F.AttributeArgument(
                                //    F.NameEquals("Pack"),
                                //    default,
                                //    F.ParseExpression("1"))
                                ]))])])
                    .WithOpenBraceToken(F.Token(SyntaxKind.OpenBraceToken))
                    .WithMembers([
                        .. Enumerable.Range(0, length).Select(i => F.FieldDeclaration(
                            F.VariableDeclaration(F.IdentifierName(type))
                                .WithVariables([F.VariableDeclarator(F.Identifier($"item{i}"))]))),
                        F.MethodDeclaration(F.RefType(F.ParseTypeName(type)), "Ref")
                            .WithAttributeLists([F.AttributeList([
                                F.Attribute(F.ParseName("global::System.Runtime.CompilerServices.MethodImplAttribute"))
                                    .WithArgumentList(F.AttributeArgumentList([
                                        F.AttributeArgument(F.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"))]))])])
                            .WithModifiers([ F.Token(SyntaxKind.PublicKeyword), F.Token(SyntaxKind.StaticKeyword) ])
                            .WithParameterList(F.ParameterList([
                                F.Parameter(F.Identifier("value"))
                                    .WithModifiers(F.TokenList(F.Token(SyntaxKind.RefKeyword)))
                                    .WithType(F.ParseTypeName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))),
                                F.Parameter(F.Identifier("i")).WithType(F.PredefinedType(F.Token(SyntaxKind.IntKeyword)))]))
                            .WithExpressionBody(F.ArrowExpressionClause(
                                F.ParseExpression("ref global::System.Runtime.CompilerServices.Unsafe.Add(ref value.item0, i)")))
                            .WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken))
                    ]).WithCloseBraceToken(F.Token(SyntaxKind.CloseBraceToken));

                if (create)
                {
                    node = node.AddMembers(
                        F.MethodDeclaration(F.ParseTypeName($"global::System.Span<{type}>"), "AsSpan")
                            .WithAttributeLists([F.AttributeList([
                                F.Attribute(F.ParseName("global::System.Runtime.CompilerServices.MethodImplAttribute"))
                                    .WithArgumentList(F.AttributeArgumentList([
                                        F.AttributeArgument(F.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"))]))])])
                            .WithModifiers([F.Token(SyntaxKind.PublicKeyword), F.Token(SyntaxKind.StaticKeyword)])
                            .WithParameterList(F.ParameterList([
                                F.Parameter(F.Identifier("value"))
                                    .WithModifiers(F.TokenList(F.Token(SyntaxKind.RefKeyword)))
                                    .WithType(F.ParseTypeName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))),
                                F.Parameter(F.Identifier("length")).WithType(F.PredefinedType(F.Token(SyntaxKind.IntKeyword))).WithDefault(F.EqualsValueClause(F.ParseExpression($"{length}")))]))
                            .WithExpressionBody(F.ArrowExpressionClause(
                                F.ParseExpression($"global::System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref value.item0, length)")))
                            .WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken))
                        );
                }
                //else
                //{
                //    node = node.AddMembers([
                //        F.MethodDeclaration(F.ParseTypeName($"global::System.Span<{type}>"), "AsSpan")
                //            .WithAttributeLists([F.AttributeList([
                //                F.Attribute(F.ParseName("global::System.Runtime.CompilerServices.MethodImplAttribute"))
                //                    .WithArgumentList(F.AttributeArgumentList([
                //                        F.AttributeArgument(F.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"))]))])])
                //            .WithModifiers([F.Token(SyntaxKind.PublicKeyword), F.Token(SyntaxKind.StaticKeyword)])
                //            .WithParameterList(F.ParameterList([
                //                F.Parameter(F.Identifier("value"))
                //                    .WithModifiers(F.TokenList(F.Token(SyntaxKind.RefKeyword)))
                //                    .WithType(F.ParseTypeName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))),
                //                F.Parameter(F.Identifier("length")).WithType(F.PredefinedType(F.Token(SyntaxKind.IntKeyword))).WithDefault(F.EqualsValueClause(F.ParseExpression($"{length}")))
                //            ])).WithExpressionBody(F.ArrowExpressionClause(
                //                F.ParseExpression($"global::System.Span<T>.DangerousCreate(null, ref value.item0, length)")))
                //            .WithSemicolonToken(F.Token(SyntaxKind.SemicolonToken)),
                //        F.FieldDeclaration(F.VariableDeclaration(F.ParseTypeName("int")).WithVariables([F.VariableDeclarator(F.Identifier("touch"))]))
                //            .WithModifiers([F.Token(SyntaxKind.PrivateKeyword), F.Token(SyntaxKind.StaticKeyword)]),
                //        F.MethodDeclaration(F.PredefinedType(F.Token(SyntaxKind.VoidKeyword)), "Touch")
                //            .WithAttributeLists([F.AttributeList([
                //                F.Attribute(F.ParseName("global::System.Runtime.CompilerServices.MethodImplAttribute"))
                //                    .WithArgumentList(F.AttributeArgumentList([
                //                        F.AttributeArgument(F.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining"))]))])])
                //            .WithModifiers([F.Token(SyntaxKind.PublicKeyword), F.Token(SyntaxKind.StaticKeyword)])
                //            .WithParameterList(F.ParameterList([
                //                F.Parameter(F.Identifier("value"))
                //                    .WithModifiers(F.TokenList(F.Token(SyntaxKind.RefKeyword)))
                //                    .WithType(F.ParseTypeName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                //            ])).WithBody(F.Block(new[] {
                //                F.ExpressionStatement(F.ParseExpression($"_ = global::System.Threading.Interlocked.Increment(ref touch)"))
                //            }))
                //        ]);
                //}

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
                    .WithUsings([F.UsingDirective(F.ParseName("SortSharp.Extensions"))])
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
}
