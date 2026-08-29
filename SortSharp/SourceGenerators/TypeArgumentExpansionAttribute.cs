using System;
using System.Diagnostics;

namespace SortSharp.SourceGenerators;

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class TypeArgumentExpansionAttribute : Attribute;
