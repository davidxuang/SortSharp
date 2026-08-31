using System.Collections;
using System.Diagnostics.CodeAnalysis;
using SortSharp.Testing;

namespace SortSharp.Tests;

[MethodDataSource(nameof(GetSettings))]
public sealed class IntegerTests(int Length, IntegerPattern Pattern)
{
    public static IEnumerable<ValueTuple<int, IntegerPattern>> GetSettings()
    {
        yield return (0, IntegerPattern.Sorted);
        yield return (1, IntegerPattern.Sorted);
        foreach (var pattern in new[] { IntegerPattern.Random, IntegerPattern.Sorted, IntegerPattern.Reverse })
        {
            foreach (var length in new[] { 2, 3, 4, 5, 7, 11, 13, 16, 32, 67, 257, 1021, 4099, 65_537, 1_000_000 })
                yield return (length, pattern);
        }
        foreach (var pattern in new[] { IntegerPattern.OrganPipe, IntegerPattern.AlmostZeros95, IntegerPattern.Random16 })
        {
            foreach (var length in new[] { 67, 257, 1021, 4099, 65_537, 1_000_000 })
                yield return (length, pattern);
        }
    }

    private long[]? _original;
    private long[]? _keys;
    private long[]? _values;

    private Span<long> Keys => _keys.AsSpan(0, Length);
    private Span<long> Values => _values.AsSpan(0, Length);

    [Before(Test)]
    [MemberNotNull(nameof(_original), nameof(_keys), nameof(_values))]
    public void Setup()
    {
        _original = new IntegerGenerator<long>().GetArray(Length, Pattern);

        _keys = new long[Length];
        Array.Copy(_original, _keys, Length);
        _values = new long[Length];
        for (int i = 0; i < Length; i++)
            _values[i] = i;
    }

    private async Task Validate(bool stable)
    {
        var seen = new BitArray(Length);

        for (int i = 0; i < Length; i++)
        {
            long o = _values![i];
            await Assert.That(o >= 0 && o < Length).IsTrue().Because($"values[{i}] should be a valid original index");
            await Assert.That(seen[(int)o]).IsFalse().Because($"original index {o} should occur exactly once");
            seen[(int)o] = true;

            await Assert.That(_keys![i]).IsEqualTo(_original![_values![i]]).Because($"keys[{i}] should correspond to original[{o}]");
                        
            if (i == 0) continue;
            await Assert.That(_keys![i]).IsGreaterThanOrEqualTo(_keys[i - 1]).Because($"keys should be sorted at index {i}");
            if (stable && _keys![i] == _keys[i - 1])
                await Assert.That(_values![i]).IsGreaterThan(_values[i - 1]).Because($"equal keys should retain their original order at index {i}");
        }
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.BranchlessProfiles))]
    public async Task IPNSort(BranchlessProfile variant)
    {
        if (variant == BranchlessProfile.Branchless)
            Keys.IPNSort(Values);
        else
            IPN.Cmp<long>.Sort(Keys, Values);

        await Validate(false);
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.BranchlessProfiles))]
    public async Task PDQSort(BranchlessProfile variant)
    {
        if (variant == BranchlessProfile.Branchless)
            Keys.PDQSort(Values);
        else
            PDQ.Cmp<long>.Sort(Keys, Values);

        await Validate(false);
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.GrailProfiles))]
    public async Task GrailSort(MemoryProfile profile)
    {
        Keys.GrailSort(Values, profile);

        await Validate(true);
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.WikiProfiles))]
    public async Task WikiSort(MemoryProfile profile)
    {
        Keys.WikiSort(Values, profile);

        await Validate(true);
    }

    [Test]
    public async Task RadixLsdSort()
    {
        Keys.RadixLsdSort(Values, 8);

        await Validate(true);
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.RafixMsdProfiles))]
    public async Task RadixMsdSort(MemoryProfile profile)
    {
        Keys.RadixMsdSort(Values, 8, profile);

        await Validate(stable: profile >= MemoryProfile.High);
    }
}
