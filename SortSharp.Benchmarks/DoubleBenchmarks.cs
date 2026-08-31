using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using SortSharp.Testing;

namespace SortSharp.Benchmarks;

[MemoryDiagnoser]
[StopOnFirstError]
[Config(typeof(Config))]
public partial class DoubleBenchmarks
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
        DoublePattern.RandomFinite,
        DoublePattern.RandomCategory,
        DoublePattern.NanTail5,
    ])]
    public DoublePattern Pattern;

    [Params([
        BfpIeeeOrder.Default,
        BfpIeeeOrder.TotalIeee754,
    ])]
    public BfpIeeeOrder Order;

    private double[] data = null;
    private double[] buffer = null;
    private double[] truth = null;
    private static readonly TotalOrderIeee754Comparer<double> totalComparer = new();

    [GlobalSetup]
    public void Setup()
    {
        data = new DoubleGenerator().GetArray(Size, Pattern);
        buffer = new double[Size];
        truth = new double[Size];
        Array.Copy(data, truth, Size);
        if (Order == BfpIeeeOrder.TotalIeee754)
            Array.Sort(truth, totalComparer);
        else
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
        if (Order == BfpIeeeOrder.TotalIeee754)
        {
            for (int i = 0; i < Size; i++)
                if (totalComparer.Compare(buffer[i], truth[i]) != 0)
                    throw new InvalidDataException();
        }
        else
        {
            for (int i = 0; i < Size; i++)
                if (buffer[i] != truth[i] && !double.IsNaN(buffer[i]))
                    throw new InvalidDataException();
        }
    }

    [Benchmark(Baseline = true)]
    public void Sort()
    {
        if (Order == BfpIeeeOrder.TotalIeee754)
            buffer.Sort(totalComparer);
        else
            buffer.Sort();
    }

    [Benchmark]
    [Arguments(BranchlessProfile.Branchy)]
    [Arguments(BranchlessProfile.Branchless)]
    public void IPNSort(BranchlessProfile Profile)
    {
        if (Order == BfpIeeeOrder.TotalIeee754)
            buffer.IPNSort(totalComparer);
        else if (Profile == BranchlessProfile.Branchless)
            buffer.IPNSort();
        else
            IPN.Cmp<double>.Sort(buffer);
    }

    [Benchmark]
    [Arguments(BranchlessProfile.Branchy)]
    [Arguments(BranchlessProfile.Branchless)]
    public void PDQSort(BranchlessProfile Profile)
    {
        if (Order == BfpIeeeOrder.TotalIeee754)
            buffer.PDQSort(totalComparer);
        else if (Profile == BranchlessProfile.Branchless)
            buffer.PDQSort();
        else
            PDQ.Cmp<double>.Sort(buffer);
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.Medium)]
    public void GrailSort(MemoryProfile Profile)
    {
        if (Order == BfpIeeeOrder.TotalIeee754)
            buffer.GrailSort(totalComparer, Profile);
        else
            buffer.GrailSort(Profile);
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.Medium)]
    [Arguments(MemoryProfile.High)]
    public void WikiSort(MemoryProfile Profile)
    {
        if (Order == BfpIeeeOrder.TotalIeee754)
            buffer.WikiSort(totalComparer, Profile);
        else
            buffer.WikiSort(Profile);
    }

    [Benchmark]
    public void RadixLsdSort()
    {
        buffer.RadixLsdSort();
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.High)]
    public void RadixMsdSort(MemoryProfile Profile)
    {
        buffer.RadixMsdSort(Profile);
    }

    private class Config : ConfigBase
    {
        public Config()
        {
            AddFilter(new Filter());
        }
    }

    private class Filter : FilterBase<DoublePattern>
    {
        protected override int GetMinimumSize(DoublePattern pattern)
            => pattern switch
            {
                DoublePattern.NanTail5 => 1 << 8,
                _ => 16,
            };

        protected override bool PredicateOverride(BenchmarkCase benchmarkCase, int size, DoublePattern pattern, object variant)
        {
            var order = (BfpIeeeOrder)benchmarkCase.Parameters.Items
                .Single(p => p.Name == nameof(Order))
                .Value;

            if (benchmarkCase.Descriptor.WorkloadMethod.Name.StartsWith(nameof(Radix)) && order != BfpIeeeOrder.TotalIeee754)
                return false;

            if (order != BfpIeeeOrder.Default && variant is BranchlessProfile.Branchless)
                return false;

            return true;
        }
    }
}
