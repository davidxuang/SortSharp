using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortSharp.Compat;

#if !NET8_0_OR_GREATER
/// <remarks><see href="https://github.com/dotnet/dotnet/blob/v11.0.100/src/runtime/src/libraries/System.Private.CoreLib/src/System/Numerics/TotalOrderIeee754Comparer.cs"/></remarks>
[SkipLocalsInit]
internal readonly struct TotalOrderIeee754Comparer<T> : IComparer<T>, IEquatable<TotalOrderIeee754Comparer<T>>
#if NET7_0_OR_GREATER
    where T : IFloatingPointIeee754<T>
#endif
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(T? x, T? y)
    {
        if (typeof(T) == typeof(float))
            return CompareIntegerSemantic(BitConverter.SingleToInt32Bits((float)(object)x!), BitConverter.SingleToInt32Bits((float)(object)y!));
        else if (typeof(T) == typeof(double))
            return CompareIntegerSemantic(BitConverter.DoubleToInt64Bits((double)(object)x!), BitConverter.DoubleToInt64Bits((double)(object)y!));
#if NETSTANDARD2_0_COMPAT
        else if (typeof(T) == typeof(Half))
            return CompareIntegerSemantic(BitConverter.HalfToInt16Bits((Half)(object)x!), BitConverter.HalfToInt16Bits((Half)(object)y!));
#endif
#if NET7_0_OR_GREATER
        return CompareGeneric(x, y);
#else
        throw new ArgumentException(nameof(T));
#endif
    }

#if NET7_0_OR_GREATER
    static int CompareIntegerSemantic<TInteger>(TInteger x, TInteger y)
        where TInteger : unmanaged, IBinaryInteger<TInteger>, ISignedNumber<TInteger>
    {
        // In IEEE 754 binary floating-point representation, a number is represented as Sign|Exponent|Significand
        // Normal numbers has an implicit 1. in front of the significand, so value with larger exponent will have larger absolute value
        // Inf and NaN are defined as Exponent=All 1s, while Inf has Significand=0, sNaN has Significand=0xxx and qNaN has Significand=1xxx
        // This also satisfies totalOrder definition which is +x < +Inf < +sNaN < +qNaN

        // The order of NaNs of same category and same sign is implementation defined,
        // here we define it as the order of exponent bits to simplify comparison

        // Negative values are represented in sign-magnitude, instead of two's complement like integers
        // Just negating the comparison result when both numbers are negative is enough

        return (TInteger.IsNegative(x) && TInteger.IsNegative(y)) ? y.CompareTo(x) : x.CompareTo(y);
    }

    static int CompareGeneric(T? x, T? y)
    {
        // IComparer contract is null < value

        if (x is null)
        {
            return (y is null) ? 0 : -1;
        }
        else if (y is null)
        {
            return 1;
        }

        // If < or > returns true, the result satisfies definition of totalOrder too

        if (x < y)
        {
            return -1;
        }
        else if (x > y)
        {
            return 1;
        }
        else if (x == y)
        {
            if (T.IsZero(x)) // only zeros are equal to zeros
            {
                // IEEE 754 numbers are either positive or negative. Skip check for the opposite.

                if (T.IsNegative(x))
                {
                    return T.IsNegative(y) ? 0 : -1;
                }
                else
                {
                    return T.IsPositive(y) ? 0 : 1;
                }
            }
            else
            {
                // Equivalant values are compared by their exponent parts,
                // and the value with smaller exponent is considered closer to zero.

                // This only applies to IEEE 754 decimals. Consider to add support if decimals are added into .NET.
                return 0;
            }
        }
        else
        {
            // One or two of the values are NaN
            // totalOrder defines that -qNaN < -sNaN < x < +sNaN < + qNaN

            static int CompareSignificand(T x, T y)
            {
                // IEEE 754 totalOrder only defines the order of NaN type bit (the first bit of significand)
                // To match the integer semantic comparison above, here we compare all the significand bits
                // Revisit this if decimals are added

                // Leave the space for custom floating-point type that has variable significand length

                int xSignificandBits = x!.GetSignificandBitLength();
                int ySignificandBits = y!.GetSignificandBitLength();

                if (xSignificandBits == ySignificandBits)
                {
                    // Prevent stack overflow for huge numbers
                    const int StackAllocThreshold = 256;

                    int xSignificandLength = x.GetSignificandByteCount();
                    int ySignificandLength = y.GetSignificandByteCount();

                    Span<byte> significandX = (uint)xSignificandLength <= StackAllocThreshold ? stackalloc byte[xSignificandLength] : new byte[xSignificandLength];
                    Span<byte> significandY = (uint)ySignificandLength <= StackAllocThreshold ? stackalloc byte[ySignificandLength] : new byte[ySignificandLength];

                    x.WriteSignificandBigEndian(significandX);
                    y.WriteSignificandBigEndian(significandY);

                    return significandX.SequenceCompareTo(significandY);
                }
                else
                {
                    return xSignificandBits.CompareTo(ySignificandBits);
                }
            }

            if (T.IsNaN(x))
            {
                if (T.IsNaN(y))
                {
                    if (T.IsNegative(x))
                    {
                        return T.IsPositive(y) ? -1 : CompareSignificand(y, x);
                    }
                    else
                    {
                        return T.IsNegative(y) ? 1 : CompareSignificand(x, y);
                    }
                }
                else
                {
                    return T.IsPositive(x) ? 1 : -1;
                }
            }
            else if (T.IsNaN(y))
            {
                return T.IsPositive(y) ? -1 : 1;
            }
            else
            {
                // T does not correctly implement IEEE754 semantics
                throw new ArgumentException(nameof(T));
            }
        }
    }
#else
    static int CompareIntegerSemantic(short x, short y)
        => (x < 0 && y < 0) ? y.CompareTo(x) : x.CompareTo(y);
    static int CompareIntegerSemantic(int x, int y)
        => (x < 0 && y < 0) ? y.CompareTo(x) : x.CompareTo(y);
    static int CompareIntegerSemantic(long x, long y)
        => (x < 0 && y < 0) ? y.CompareTo(x) : x.CompareTo(y);
#endif

    public bool Equals(TotalOrderIeee754Comparer<T> other) => true;
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TotalOrderIeee754Comparer<T>;
    public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode();
}
#endif
