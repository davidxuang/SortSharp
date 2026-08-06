using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SortSharp.Extensions;

internal static class RuntimeExtensions
{
    extension(ArgumentOutOfRangeException)
    {
        internal static void ThrowIf(bool condition, string? paramName = default)
        {
            if (condition) throw new ArgumentOutOfRangeException(paramName);
        }
    }

#if !NET7_0_OR_GREATER
    extension(ArgumentNullException)
    {
        internal static void ThrowIfNull(object? value, string? paramName = default)
        {
            if (value is null) throw new ArgumentNullException(paramName);
        }
    }
#endif

#if !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP2_1_OR_GREATER
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
        if (type.IsPrimitive) return false;
        if (!type.IsValueType) return true;
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPrimitive || type.IsEnum) return false;
        return type.GetTypeInfo().DeclaredFields.Any(f => !f.IsStatic && IsReferenceOrContainsReferences(f.FieldType));
    }
#endif
}

#if !NETCOREAPP3_0_OR_GREATER
internal static class BitOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Log2(uint value)
    {
        int log = 0;
        while ((value >>= 1) != 0) log++;
        return log;
    }
}
#endif
