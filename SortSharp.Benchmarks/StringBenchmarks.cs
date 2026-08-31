using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using SortSharp.Testing;

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

    [Params([
        StringOrder.Default,
        StringOrder.Ordinal,
    ])]
    public StringOrder Order;

    private string[] data = null;
    private string[] buffer = null;
    private string[] truth = null;

    [GlobalSetup]
    public void Setup()
    {
        data = new StringGenerator().GetArray(Size, Pattern);
        buffer = new string[Size];
        truth = new string[Size];
        Array.Copy(data, truth, Size);
        if (Order == StringOrder.Ordinal)
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
        if (Order == StringOrder.Ordinal)
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
        if (Order == StringOrder.Ordinal)
            buffer.Sort(StringComparer.Ordinal);
        else
            buffer.Sort();
    }

    [Benchmark]
    public void IPNSort()
    {
        if (Order == StringOrder.Ordinal)
            buffer.IPNSort(StringComparer.Ordinal);
        else
            buffer.IPNSort();
    }

    [Benchmark]
    public void PDQSort()
    {
        if (Order == StringOrder.Ordinal)
            buffer.PDQSort(StringComparer.Ordinal);
        else
            buffer.PDQSort();
    }

    [Benchmark]
    [Arguments(MemoryProfile.Minimum)]
    [Arguments(MemoryProfile.Baseline)]
    [Arguments(MemoryProfile.Medium)]
    public void GrailSort(MemoryProfile Profile)
    {
        if (Order == StringOrder.Ordinal)
            buffer.GrailSort(StringComparer.Ordinal, Profile);
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
        if (Order == StringOrder.Ordinal)
            buffer.WikiSort(StringComparer.Ordinal, Profile);
        else
            buffer.WikiSort(Profile);
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

    private class Filter : FilterBase<StringPattern>
    {
        protected override int GetMinimumSize(StringPattern pattern)
            => pattern switch
            {
                StringPattern.NounZipf1 => 1000,
                _ => 16,
            };

        protected override bool PredicateOverride(BenchmarkCase benchmarkCase, int size, StringPattern pattern, object variant)
        {
            var order = (StringOrder)benchmarkCase.Parameters.Items
                .Single(p => p.Name == nameof(Order))
                .Value;

            if (benchmarkCase.Descriptor.WorkloadMethod.Name.StartsWith(nameof(Radix)) && order != StringOrder.Ordinal)
                return false;

            return true;
        }
    }
}
