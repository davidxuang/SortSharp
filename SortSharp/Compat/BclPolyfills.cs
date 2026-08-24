using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SortSharp.Compat;

internal static partial class BclPolyfills
{
    extension(ArgumentException)
    {
        internal static void ThrowIf(bool predicate, string? message = default, string? paramName = default)
        {
            if (predicate) throw new ArgumentException(message, paramName);
        }
    }

#if !NET6_0_OR_GREATER
    extension(ArgumentNullException)
    {
        internal static void ThrowIfNull(object? value, [CallerArgumentExpression(nameof(value))] string? paramName = default)
        {
            if (value is null) throw new ArgumentNullException(paramName);
        }
    }
#endif

#if !NET8_0_OR_GREATER
    extension(ArgumentOutOfRangeException)
    {
        internal static void ThrowIfNotEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = default)
            where T : IEquatable<T>
        {
            if (!value.Equals(other))
                throw new ArgumentOutOfRangeException(paramName);
        }

        internal static void ThrowIfLessThan<T>(T value, T min, [CallerArgumentExpression(nameof(value))] string? paramName = default)
            where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
                throw new ArgumentOutOfRangeException(paramName);
        }

        internal static void ThrowIfGreaterThan<T>(T value, T max, [CallerArgumentExpression(nameof(value))] string? paramName = default)
            where T : IComparable<T>
        {
            if (value.CompareTo(max) > 0)
                throw new ArgumentOutOfRangeException(paramName);
        }
    }
#endif

#if NETSTANDARD && !NETSTANDARD2_0_OR_GREATER
    extension(Type type)
    {
        internal bool IsValueType => type.GetTypeInfo().IsValueType;
        internal bool IsAssignableFrom(Type? c) => type.GetTypeInfo().IsAssignableFrom(c?.GetTypeInfo() ?? throw new ArgumentNullException(nameof(c)));
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
