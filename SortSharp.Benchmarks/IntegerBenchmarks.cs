using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace SortSharp.Benchmarks;

[MemoryDiagnoser]
[StopOnFirstError]
[Config(typeof(Config))]
public partial class IntegerBenchmarks
{
    [Params([
        100,
        1_000,
        10_000,
        100_000,
        1_000_000,
    ])]
    public int Size;

    [Params([
        IntegerPattern.Random,
        IntegerPattern.Sorted,
        IntegerPattern.Reverse,
        IntegerPattern.SortedHead95,
        IntegerPattern.Noisy,
        IntegerPattern.OrganPipe,
        IntegerPattern.Stagger,
        IntegerPattern.Sawtooth61,
        IntegerPattern.AllZeros,
        IntegerPattern.AlmostZeros95,
        IntegerPattern.Random16,
        IntegerPattern.Zipf1,
    ])]
    public IntegerPattern Pattern;

    private nint[] data = null;
    private nint[] buffer = null;
    private nint[] truth = null;

    [GlobalSetup]
    public void Setup()
    {
        data = IntegerGenerator<nint>.GetArray(Size, Pattern);
        buffer = new nint[Size];
        truth = new nint[Size];
        Array.Copy(data, truth, Size);
        Array.Sort(truth);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        Array.Copy(data, buffer, Size);
    }

    [GlobalCleanup]
    public void Validate()
    {
        if (!buffer.SequenceEqual(truth))
            throw new InvalidDataException();
    }

    [Benchmark(Baseline = true)]
    public void Sort()
    {
        buffer.Sort();
    }

    [Benchmark]
    [Arguments(BranchlessVariant.Branchy)]
    [Arguments(BranchlessVariant.Branchless)]
    public void IPNSort(BranchlessVariant Variant)
    {
        if (Variant == BranchlessVariant.Branchless)
            buffer.IPNSort();
        else
            IPN.Cmp<nint>.Sort(buffer);
    }

    [Benchmark]
    [Arguments(BranchlessVariant.Branchy)]
    [Arguments(BranchlessVariant.Branchless)]
    public void PDQSort(BranchlessVariant Variant)
    {
        if (Variant == BranchlessVariant.Branchless)
            buffer.PDQSort();
        else
            PDQ.Cmp<nint>.Sort(buffer);
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.High)]
    public void GrailSort(MemoryProfile Variant)
    {
        buffer.GrailSort(Variant);
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.High)]
    [Arguments(MemoryProfile.Maximum)]
    public void WikiSort(MemoryProfile Variant)
    {
        buffer.WikiSort(Variant);
    }

    private class Config : ConfigBase
    {
        public Config()
        {
            AddFilter(new Filter());
        }
    }

    private class Filter : FilterBase<IntegerPattern>
    {
        protected override int GetMinimumSize(IntegerPattern pattern)
            => pattern switch
            {
                IntegerPattern.Noisy => 1 << 8,
                // E[run length] = sqrt(n); 4096 is the first power of two
                // whose expected run length reaches 64.
                IntegerPattern.RandomRuns => 1 << 12,
                // Stagger is primarily a partition and pivot-selection pattern.
                IntegerPattern.Stagger => 1 << 8,
                // Two complete periods of the 61-element saw fit in 128.
                IntegerPattern.Sawtooth61 => 1_000,
                // Give the 5% tail and each of the 16 uniform values enough
                // observations to form a meaningful distribution.
                IntegerPattern.SortedHead95 or
                IntegerPattern.AlmostZeros95 or
                IntegerPattern.Random16 => 1 << 8,
                // A thousand ranks is enough for a visible Zipf long tail.
                IntegerPattern.Zipf1 => 1_000,
                _ => 16,
            };
    }
}

