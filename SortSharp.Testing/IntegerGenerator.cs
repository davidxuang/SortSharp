using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SortSharp.Testing;

public enum IntegerPattern
{
    Random,
    Sorted,
    Reverse,

    SortedHead95,
    Noisy,
    RandomRuns,

    OrganPipe,
    Stagger,
    Sawtooth61,

    AllZeros,
    AlmostZeros95,
    Random16,
    Zipf1,
}

public readonly struct IntegerGenerator<T>()
#if NET7_0_OR_GREATER
    where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
#else
    where T : unmanaged, IComparable<T>
#endif
{
    private readonly byte[] bytes = new byte[Unsafe.SizeOf<T>()];

    public T[] GetArray(int length, IntegerPattern pattern)
    {
        var array = new T[length];
        var random = new Random(42);

        switch (pattern)
        {
            case IntegerPattern.Random:
                for (int i = 0; i < length; i++)
                    array[i] = Next(random);
                break;
            case IntegerPattern.Sorted:
                for (int i = 0; i < length; i++)
                    array[i] = CreateChecked(i);
                break;
            case IntegerPattern.Reverse:
                for (int i = 0; i < length; i++)
                    array[i] = CreateChecked(length - i);
                break;
            case IntegerPattern.SortedHead95:
                for (int i = 0; i < length; i++)
                    array[i] = Next(random);
                Array.Sort(array, 0, length * 19 / 20);
                break;
            case IntegerPattern.RandomRuns:
                for (int i = 0; i < length; i++)
                    array[i] = CreateChecked(i);
                random.Shuffle(array);
                double prob = 1.0 / Math.Sqrt(length);
                for (int i = 0; i < length; )
                {
                    int run = NextGeometric(random, prob);
                    run = Math.Min(run, length - i);
                    Array.Sort(array, i, run);
                    i += run;
                }
                break;
            case IntegerPattern.Noisy:
                int window = Math.Max(4, (int)Math.Sqrt(length) / 2);
                for (int i = 0; i < length; i++)
                    array[i] = CreateChecked(i);
                int s = 0;
                while (s + window <= length)
                {
                    random.Shuffle(array.AsSpan(s, window));
                    s += random.Next(1, window);
                }
                random.Shuffle(array.AsSpan(s));
                break;
            case IntegerPattern.OrganPipe:
                for (int i = 0; i < length / 2; i++)
                    array[i] = CreateChecked(i);
                for (int i = length / 2; i < length; i++)
                    array[i] = CreateChecked(length - i);
                break;
            case IntegerPattern.Stagger:
                int step = length / 2 + 1;
                for (int i = 0; i < length; i++)
                    array[i] = CreateChecked((long)i * step % length);
                break;
            case IntegerPattern.Sawtooth61:
                for (int i = 0; i < length; i++)
                    array[i] = CreateChecked(i % 61);
                break;
            case IntegerPattern.AllZeros:
                for (int i = 0; i < length; i++)
                    array[i] = Zero;
                break;
            case IntegerPattern.AlmostZeros95:
                for (int i = 0; i < length; i++)
                {
                    if (random.Next(20) == 0)
                        array[i] = Next(random);
                    else
                        array[i] = Zero;
                }
                break;
            case IntegerPattern.Random16:
                for (int i = 0; i < length; i++)
                    array[i] = CreateChecked(random.Next(16));
                break;
            case IntegerPattern.Zipf1:
                var zipf = new ZipfSampler(CreateChecked(MaxValue), 1.0);
                for (int i = 0; i < length; i++)
                {
                    long value = zipf.Next(random);
                    array[i] = CreateChecked(value);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        return array;
    }

    private T Next(Random random)
    {
        random.NextBytes(bytes);
        return MemoryMarshal.Read<T>(bytes);
    }

    private static int NextGeometric(Random random, double probability)
    {
        // Geometric distribution with support [1, +∞).
        double u = random.NextDouble();

        return 1 + (int)(
            Math.Log(1.0 - u) /
            Math.Log(1.0 - probability));
    }

    private static T CreateChecked(int value)
#if NET7_0_OR_GREATER
        => T.CreateChecked(value);
#else
        => (T)Convert.ChangeType(value, typeof(T));
#endif

    private static T CreateChecked(long value)
#if NET7_0_OR_GREATER
        => T.CreateChecked(value);
#else
        => (T)Convert.ChangeType(value, typeof(T));
#endif

    private static long CreateChecked(T value)
#if NET7_0_OR_GREATER
        => long.CreateChecked(value);
#else
        => (long)Convert.ChangeType(value, typeof(long));
#endif

    private static T Zero
#if NET7_0_OR_GREATER
        => T.Zero;
#else
        => (T)Convert.ChangeType(0, typeof(T));
#endif

    private static T MaxValue
#if NET7_0_OR_GREATER
        => T.MaxValue;
#else
    {
        get
        {
            if (typeof(T) == typeof(sbyte)) return (T)(object)sbyte.MaxValue;
            if (typeof(T) == typeof(byte)) return (T)(object)byte.MaxValue;
            if (typeof(T) == typeof(short)) return (T)(object)short.MaxValue;
            if (typeof(T) == typeof(ushort)) return (T)(object)ushort.MaxValue;
            if (typeof(T) == typeof(int)) return (T)(object)int.MaxValue;
            if (typeof(T) == typeof(uint)) return (T)(object)uint.MaxValue;
            if (typeof(T) == typeof(long)) return (T)(object)long.MaxValue;
            if (typeof(T) == typeof(ulong)) return (T)(object)ulong.MaxValue;
            throw new NotSupportedException();
        }
    }
#endif
}
