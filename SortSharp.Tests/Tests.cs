namespace SortSharp.Tests;

public class Tests
{
    private static void Prepare(Span<long> span)
    {
        for (int i = 0; i < span.Length; i++)
            span[i] = i / 2;

        var random = new Random(42);
#if NET8_0_OR_GREATER
        random.Shuffle(span);
#else
        for (int i = span.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (span[j], span[i]) = (span[i], span[j]);
        }
#endif
    }

    [Test]
    [Arguments(10_000, BranchlessVariant.Branchy)]
    [Arguments(10_000, BranchlessVariant.Branchless)]
    public async Task IPNSort(int length, BranchlessVariant variant)
    {
        var keys = new long[length];
        Prepare(keys);
        var values = new long[length];
        keys.CopyTo(values);

        if (variant == BranchlessVariant.Branchless)
            keys.IPNSort(values);
        else
            IPN.Cmp<long>.Sort(keys, values);

        for (int i = 0; i < keys.Length; i++)
        {
            await Assert.That(keys[i]).EqualTo(i / 2).Because($"index {i}");
            await Assert.That(values[i]).EqualTo(i / 2).Because($"index {i}");
        }
    }

    [Test]
    [Arguments(10_000, BranchlessVariant.Branchy)]
    [Arguments(10_000, BranchlessVariant.Branchless)]
    public async Task PDQSort(int length, BranchlessVariant variant)
    {
        var keys = new long[length];
        Prepare(keys);
        var values = new long[length];
        keys.CopyTo(values);

        if (variant == BranchlessVariant.Branchless)
            keys.PDQSort(values);
        else
            PDQ.Cmp<long>.Sort(keys, values);

        for (int i = 0; i < keys.Length; i++)
        {
            await Assert.That(keys[i]).EqualTo(i / 2).Because($"index {i}");
            await Assert.That(values[i]).EqualTo(i / 2).Because($"index {i}");
        }
    }

    [Test]
    [Arguments(10_000, MemoryProfile.Minimum)]
    [Arguments(10_000, MemoryProfile.Baseline)]
    [Arguments(10_000, MemoryProfile.High)]
    public async Task GrailSort(int length, MemoryProfile profile)
    {
        var keys = new long[length];
        Prepare(keys);
        var values = new long[length];
        keys.CopyTo(values);

        keys.GrailSort(values, profile);

        for (int i = 0; i < keys.Length; i++)
        {
            await Assert.That(keys[i]).EqualTo(i / 2).Because($"index {i}");
            await Assert.That(values[i]).EqualTo(i / 2).Because($"index {i}");
        }
    }

    [Test]
    [Arguments(10_000, MemoryProfile.Minimum)]
    [Arguments(10_000, MemoryProfile.Baseline)]
    [Arguments(10_000, MemoryProfile.High)]
    [Arguments(10_000, MemoryProfile.Maximum)]
    public async Task WikiSort(int length, MemoryProfile profile)
    {
        var keys = new long[length];
        Prepare(keys);
        var values = new long[length];
        keys.CopyTo(values);

        keys.WikiSort(values, profile);

        for (int i = 0; i < keys.Length; i++)
        {
            await Assert.That(keys[i]).EqualTo(i / 2).Because($"index {i}");
            await Assert.That(values[i]).EqualTo(i / 2).Because($"index {i}");
        }
    }
}

public enum BranchlessVariant
{
    Branchy,
    Branchless,
}
