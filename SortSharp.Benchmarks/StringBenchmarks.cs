using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace SortSharp.Benchmarks;

[MemoryDiagnoser]
[StopOnFirstError]
[Config(typeof(Config))]
public partial class StringBenchmarks
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
        StringPattern.Ascii64,
        StringPattern.Ascii64Prefix48,
        StringPattern.Dna32,
        StringPattern.NounZipf1,
    ])]
    public StringPattern Pattern;

    public enum OrderType
    {
        Default,
        Ordinal,
    }

    [Params([
        OrderType.Default,
        OrderType.Ordinal,
    ])]
    public OrderType Order;

    private string[] data = null;
    private string[] buffer = null;
    private string[] truth = null;

    [GlobalSetup]
    public void Setup()
    {
        data = StringGenerator.GetArray(Size, Pattern);
        buffer = new string[Size];
        truth = new string[Size];
        Array.Copy(data, truth, Size);
        if (Order == OrderType.Ordinal)
            Array.Sort(truth, StringComparer.Ordinal);
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
        if (Order == OrderType.Ordinal)
        {
            for (int i = 0; i < Size; i++)
                if (StringComparer.Ordinal.Compare(buffer[i], truth[i]) != 0)
                    throw new InvalidDataException();
        }
        else
        {
            for (int i = 0; i < Size; i++)
                if (buffer[i].CompareTo(truth[i]) != 0)
                    throw new InvalidDataException();
        }
    }

    [Benchmark(Baseline = true)]
    public void Sort()
    {
        if (Order == OrderType.Ordinal)
            buffer.Sort(StringComparer.Ordinal);
        else
            buffer.Sort();
    }

    [Benchmark]
    public void IPNSort()
    {
        if (Order == OrderType.Ordinal)
            buffer.IPNSort(StringComparer.Ordinal);
        else
            buffer.IPNSort();
    }

    [Benchmark]
    public void PDQSort()
    {
        if (Order == OrderType.Ordinal)
            buffer.PDQSort(StringComparer.Ordinal);
        else
            buffer.PDQSort();
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.High)]
    public void GrailSort(MemoryProfile Variant)
    {
        if (Order == OrderType.Ordinal)
            buffer.GrailSort(StringComparer.Ordinal, Variant);
        else
            buffer.GrailSort(Variant);
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.High)]
    [Arguments(MemoryProfile.Maximum)]
    public void WikiSort(MemoryProfile Variant)
    {
        if (Order == OrderType.Ordinal)
            buffer.WikiSort(StringComparer.Ordinal, Variant);
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

    private class Filter : FilterBase<StringPattern>
    {
        protected override int GetMinimumSize(StringPattern pattern)
            => pattern switch
            {
                StringPattern.NounZipf1 => 1000,
                _ => 16,
            };
    }
}
