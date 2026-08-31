using System;
using System.Runtime.InteropServices;

namespace SortSharp.Testing;

internal static class Polyfills
{
    extension(ArgumentOutOfRangeException)
    {
        public static void ThrowIfNegativeOrZero(long value, string? paramName = null)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be positive.");
        }
    }

    extension(Random random)
    {
        public void Shuffle<T>(Span<T> span)
        {
            for (int i = span.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (span[i], span[j]) = (span[j], span[i]);
            }
        }
    }

    extension(Math)
    {
        public static long Clamp(long value, long min, long max) => value switch
        {
            _ when value < min => min,
            _ when value > max => max,
            _ => value
        };

        public static double Clamp(double value, double min, double max) => value switch
        {
            _ when value < min => min,
            _ when value > max => max,
            _ => value
        };
    }

    extension(double)
    {
        public static double NegativeZero => -0.0d;

        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    extension(char)
    {
        public static bool IsAsciiLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';
    }

    extension(ReadOnlySpan<char> span)
    {
        public unsafe string CreateString()
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            return new string(span);
#else
            fixed (char* ptr = &MemoryMarshal.GetReference(span))
            {
                return new string(ptr, 0, span.Length);
            }
#endif
        }
    }
}
