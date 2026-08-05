using System;
using System.Diagnostics;

namespace SortSharp.SourceGeneration;

#pragma warning disable CS9113

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class TemplateAttribute(string generic, string? compare, params string[] data) : Attribute
{
    public TemplateVariants Switch { get; set; }
}

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class TemplateClassAttribute : Attribute
{
    public bool IsUnstable { get; set; }
    public TemplateVariants Switch { get; set; }
}
