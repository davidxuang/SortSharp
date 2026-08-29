using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SortSharp.Foundation;

internal static class ThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static byte ThrowUnreachable()
#if NET7_0_OR_GREATER
        => throw new UnreachableException();
#else
        => throw new InvalidOperationException("An unreachable code path was reached.");
#endif

    extension(ArgumentException)
    {
        internal static void ThrowIf(bool condition, string? message = null, string? paramName = null)
        {
            if (condition) ThrowArgument(message, paramName);
        }

        internal static string MsgInvalidArgumentForComparison => "Type of argument is not compatible with the generic comparer.";
        internal static string MsgSpanOverlaps => "The input spans should not overlap with each other.";
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgument(string? message, string? paramName)
        => throw new ArgumentException(message, paramName);

#if !NET6_0_OR_GREATER
    extension(ArgumentNullException)
    {
        internal static void ThrowIfNull(object? value, [CallerArgumentExpression(nameof(value))] string? paramName = default)
        {
            if (value is null) ThrowArgumentNull(paramName);
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentNull(string? paramName)
        => throw new ArgumentNullException(paramName);
#endif

#if !NET8_0_OR_GREATER
    extension(ArgumentOutOfRangeException)
    {
        internal static void ThrowIfLessThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = default)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) < 0) ThrowArgumentLess(value, other, paramName);
        }

        internal static void ThrowIfGreaterThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = default)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) > 0) ThrowArgumentGreater(value, other, paramName);
        }

        internal static void ThrowIfNotEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = default)
            where T : IEquatable<T>
        {
            if (!value.Equals(other)) ThrowArgumentNotEqual(value, other, paramName);
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentLess<T>(T value, T other, string? paramName = default)
        => throw new ArgumentOutOfRangeException(string.Format("{0} ('{1}') must be less than '{2}'.", paramName, value, other), paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentGreater<T>(T value, T other, string? paramName = default)
        => throw new ArgumentOutOfRangeException(string.Format("{0} ('{1}') must be greater than '{2}'.", paramName, value, other), paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentNotEqual<T>(T value, T other, string? paramName = default)
        => throw new ArgumentOutOfRangeException(string.Format("{0} ('{1}') must be equal to '{2}'.", paramName, value, other), paramName);
#endif

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidOperation(string? message)
    {
        throw new InvalidOperationException(message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowNotSupported(string? message)
    {
        throw new NotSupportedException(message);
    }
}
