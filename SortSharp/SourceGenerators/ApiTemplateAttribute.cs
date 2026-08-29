using System;
using System.Diagnostics;

namespace SortSharp.SourceGenerators;

#pragma warning disable CS9113

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class ApiTemplateAttribute(string type) : Attribute
{
    public bool KeySelector { get; set; } = false;
}
