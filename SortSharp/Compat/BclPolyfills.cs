using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SortSharp.Foundation;

namespace SortSharp.Compat;

internal static partial class BclPolyfills
{
#if NETSTANDARD && !NETSTANDARD2_0_OR_GREATER
    extension(Type type)
    {
        internal bool IsValueType => type.GetTypeInfo().IsValueType;
        internal bool IsAssignableFrom(Type? c)
        {
            ArgumentNullException.ThrowIfNull(c);
            return type.GetTypeInfo().IsAssignableFrom(c?.GetTypeInfo());
        }
    }
#endif

#if !NETSTANDARD2_1_COMPAT
    extension(RuntimeHelpers)
    {
        internal static bool IsReferenceOrContainsReferences<T>()
            => TypeTraits<T>.IsReferenceOrContainsReferences;
    }

    private static class TypeTraits<T>
    {
        public static readonly bool IsReferenceOrContainsReferences = IsReferenceOrContainsReferences(typeof(T));
    }

    private static bool IsReferenceOrContainsReferences(Type type)
    {
#if NETSTANDARD && !NETSTANDARD2_0_OR_GREATER
        var info = type.GetTypeInfo();
        if (info.IsPrimitive) return false;
        if (!info.IsValueType) return true;
        if (Nullable.GetUnderlyingType(type) is Type t)
        {
            type = t;
            if (info.IsPrimitive) return false;
        }
        if (info.IsEnum) return false;
        return info.DeclaredFields.Any(f => !f.IsStatic && IsReferenceOrContainsReferences(f.FieldType));
#else
        if (type.IsPrimitive) return false;
        if (!type.IsValueType) return true;
        if (Nullable.GetUnderlyingType(type) is Type t)
        {
            type = t;
            if (type.IsPrimitive) return false;
        }
        if (type.IsEnum) return false;
        return type.GetTypeInfo().DeclaredFields.Any(f => !f.IsStatic && IsReferenceOrContainsReferences(f.FieldType));
#endif
    }
#endif
}
