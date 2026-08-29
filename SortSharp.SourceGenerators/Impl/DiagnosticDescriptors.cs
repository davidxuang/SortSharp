using Microsoft.CodeAnalysis;

namespace SortSharp.SourceGenerators.Impl;

#pragma warning disable RS2008

internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor Exception = new(
        "IOG000",
        "Unexpected exception",
        "{0} occurred while processing the method '{1}'",
        nameof(ImplOverloadGenerator),
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnkownType = new(
        "IOG001",
        "Unknown type",
        "Can not find the type {0}",
        nameof(ImplOverloadGenerator),
        DiagnosticSeverity.Error,
        true);
}
