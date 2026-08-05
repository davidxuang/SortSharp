using System;
using System.Diagnostics;

namespace SortSharp.SourceGeneration;

#pragma warning disable CS9113
[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal class GenerateInlineArrayAttribute(string type, int length) : Attribute;
