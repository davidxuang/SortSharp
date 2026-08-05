using System;
using System.IO;
using System.Linq;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;

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

    public enum OrderType
    {
        Default,
        TotalIeee754,
    }

    [Params([
        OrderType.Default,
        OrderType.TotalIeee754,
    ])]
    public OrderType Order;

    private double[] data = null;
    private double[] buffer = null;
    private double[] truth = null;
    private static readonly TotalOrderIeee754Comparer<double> totalComparer = new();

    [GlobalSetup]
    public void Setup()
    {
        data = DoubleGenerator.GetArray(Size, Pattern);
        buffer = new double[Size];
        truth = new double[Size];
        Array.Copy(data, truth, Size);
        if (Order == OrderType.TotalIeee754)
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
        if (Order == OrderType.TotalIeee754)
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
        if (Order == OrderType.TotalIeee754)
            buffer.Sort(totalComparer);
        else
            buffer.Sort();
    }

    [Benchmark]
    [Arguments(BranchlessVariant.Branchy)]
    [Arguments(BranchlessVariant.Branchless)]
    public void IPNSort(BranchlessVariant Variant)
    {
        if (Order == OrderType.TotalIeee754)
            buffer.IPNSort(totalComparer);
        else if (Variant == BranchlessVariant.Branchless)
            buffer.IPNSort();
        else
            IPN.Cmp<double>.Sort(buffer);
    }

    [Benchmark]
    [Arguments(BranchlessVariant.Branchy)]
    [Arguments(BranchlessVariant.Branchless)]
    public void PDQSort(BranchlessVariant Variant)
    {
        if (Order == OrderType.TotalIeee754)
            buffer.PDQSort(totalComparer);
        else if (Variant == BranchlessVariant.Branchless)
            buffer.PDQSort();
        else
            PDQ.Cmp<double>.Sort(buffer);
    }

    [Benchmark]
    [Arguments(MemoryPolicy.None)]
    [Arguments(MemoryPolicy.Fixed)]
    [Arguments(MemoryPolicy.Balanced)]
    [Arguments(MemoryPolicy.Maximum)]
    public void WikiSort(MemoryPolicy Variant)
    {
        if (Order == OrderType.TotalIeee754)
            buffer.WikiSort(totalComparer, Variant);
        else
            buffer.WikiSort(Variant);
    }

    private class Config : ConfigBase
    {
        public Config()
        {
            AddFilter(new Filter());
        }
    }

    private class Filter : IFilter
    {
        private static int MinimumSize(DoublePattern pattern)
            => pattern switch
            {
                DoublePattern.NanTail5 => 1 << 8,
                _ => 16,
            };

        public bool Predicate(BenchmarkCase benchmarkCase)
        {
            int size = (int)benchmarkCase.Parameters.Items
                .Single(p => p.Name == nameof(Size))
                .Value;
            var order = (OrderType)benchmarkCase.Parameters.Items
                .Single(p => p.Name == nameof(Order))
                .Value;
            var pattern = (DoublePattern)benchmarkCase.Parameters.Items
                .Single(p => p.Name == nameof(Pattern))
                .Value;
            object variant = benchmarkCase.Parameters.Items
                .FirstOrDefault(p => p.Name == "Variant")
                ?.Value;

            if (size < MinimumSize(pattern))
                return false;

            if (order != OrderType.Default && variant is BranchlessVariant.Branchless)
                return false;

            // filter policies
            if (variant is MemoryPolicy policy)
            {
                return policy switch
                {
                    MemoryPolicy.Maximum => (size + 1) / 2 > 512,
                    MemoryPolicy.Balanced => (int)Math.Sqrt((size + 1) / 2) + 1 > 512,
                    _ => true,
                };
            }

            return true;
        }
    }
}
