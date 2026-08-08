using System;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace SortSharp.Benchmarks;

internal abstract class ConfigBase : ManualConfig
{
    public ConfigBase(int[] sizes)
    {
        Orderer = new Orderer();

        var job = Job.Default
            .WithEnvironmentVariable("DOTNET_TieredPgo", "1")
            .WithMinIterationCount(16)
            .WithMaxIterationCount(256)
            .WithMaxRelativeError(0.01);

        foreach(var count in sizes
            .Select(GetWarpupCount)
            .Distinct()
            .Order())
        {
            AddJob(job.WithId($"W{count}")
                .WithMinWarmupCount(count)
                .WithMaxWarmupCount(count * 4));
        }

        HideColumns(
            "Job",
            "MinWarmupIterationCount",
            "MaxWarmupIterationCount");
        // WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    private static int GetWarpupCount(int size)
    {
        size = Math.Max(size, 2);
        return Math.Max((int)Math.Ceiling(Math.Pow(2, 25) / size / Math.Log2(size)), 16);
    }

    protected static int[] GetSizesFromField(FieldInfo field)
    {
        if (field.FieldType != typeof(int))
            throw new ArgumentException(
                $"Field '{field.Name}' must be of type {nameof(Int32)}.",
                nameof(field));
        var attribute = field.GetCustomAttribute<ParamsAttribute>()
            ?? throw new ArgumentException(
                $"Field '{field.Name}' does not have a {nameof(ParamsAttribute)}.",
                nameof(field));
        return [.. attribute.Values.Cast<int>()];
    }
    internal static string GetJobIdBySize(int size)
        => $"W{GetWarpupCount(size)}";
}

internal abstract class FilterBase<T> : IFilter
{
    public bool Predicate(BenchmarkCase benchmarkCase)
    {
        int size = (int)benchmarkCase.Parameters.Items
            .Single(p => p.Name == "Size")
            .Value;
        T pattern = (T)benchmarkCase.Parameters.Items
            .Single(p => p.Name == "Pattern")
            .Value;
        object variant = benchmarkCase.Parameters.Items
            .FirstOrDefault(p => p.Name == "Variant")
            ?.Value;

        if (ConfigBase.GetJobIdBySize(size) != benchmarkCase.Job.Id)
            return false;

        if (size < GetMinimumSize(pattern))
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

        return PredicateOverride(benchmarkCase, size, pattern, variant);
    }

    protected abstract int GetMinimumSize(T pattern);
    protected virtual bool PredicateOverride(BenchmarkCase benchmarkCase,
        int size,
        T pattern,
        object variant)
        => true;
}

public enum BranchlessVariant
{
    Branchy,
    Branchless,
}
