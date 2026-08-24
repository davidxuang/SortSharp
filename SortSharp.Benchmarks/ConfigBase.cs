using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace SortSharp.Benchmarks;

internal abstract class ConfigBase : ManualConfig
{
    public ConfigBase()
    {
        Orderer = new Orderer();

        AddJob(Job.Default
            .WithEnvironmentVariable("MinWarmupTime", "4000")
            .WithEngineFactory(new TimedWarmupEngineFactory())
            .WithWarmupCount(2)
            .WithMinIterationCount(16)
            .WithMaxIterationCount(256)
            .WithMaxRelativeError(0.01));

        // WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}

public sealed class TimedWarmupEngineFactory : IEngineFactory
{
    private sealed class HostWrapper(IHost _host) : IHost
    {
        public bool Drop { get; set; }

        public void Dispose() => _host.Dispose();
        public void ReportResults(RunResults runResults) => _host.ReportResults(runResults);
        public void SendError(string message) => _host.SendError(message);
        public void SendSignal(HostSignal hostSignal) => _host.SendSignal(hostSignal);
        public void Write(string message) { if (!Drop) _host.Write(message); }
        public void WriteLine() { if (!Drop) _host.WriteLine(); }
        public void WriteLine(string message) { if (!Drop) _host.WriteLine(message); }
    }

    public IEngine CreateReadyToRun(EngineParameters parameters)
    {
        var minimumTime = parameters.TargetJob.Environment.EnvironmentVariables?.FirstOrDefault(v => v.Key == "MinWarmupTime")?.Value is string s && int.TryParse(s, out var ms)
            ? TimeSpan.FromMilliseconds(ms)
            : TimeSpan.FromSeconds(4);
        var clock = parameters.TargetJob.ResolveValue(
            InfrastructureMode.ClockCharacteristic,
            EngineParameters.DefaultResolver);

        var host = parameters.Host;
        var wrapper = new HostWrapper(host);
        parameters.Host = wrapper;
        IEngine engine = new EngineFactory().CreateReadyToRun(parameters);

        wrapper.Drop = true;
        try
        {
            int index;
            Measurement measurement = default;
            var stopwatch = Stopwatch.StartNew();
            for (index = 1; stopwatch.Elapsed <= minimumTime; index++)
            {
                measurement = engine.RunIteration(new IterationData(IterationMode.Workload, IterationStage.Warmup, index, 1, 1));
                if (BitOperations.IsPow2(index)) host.WriteLine(measurement.ToString());
            }
            if (!BitOperations.IsPow2(index - 1)) host.WriteLine(measurement.ToString());
            host.WriteLine();
        }
        finally
        {
            wrapper.Drop = false;
        }

        return engine;
    }
}

internal abstract class FilterBase<T> : IFilter
{
    public bool Predicate(BenchmarkCase benchmarkCase)
    {
        string name = benchmarkCase.Descriptor.WorkloadMethod.Name;
        int size = (int)benchmarkCase.Parameters.Items
            .Single(p => p.Name == "Size")
            .Value;
        T pattern = (T)benchmarkCase.Parameters.Items
            .Single(p => p.Name == "Pattern")
            .Value;
        object variant = benchmarkCase.Parameters.Items
            .FirstOrDefault(p => p.Name == "Profile")
            ?.Value;

        if (name.StartsWith(nameof(Radix)) && size < 256)
            return false;

        if (size < GetMinimumSize(pattern))
            return false;

        // filter policies
        if (variant is MemoryProfile profile)
        {
            if (profile switch
            {
                MemoryProfile.High when name.StartsWith(nameof(Radix)) => false, // stable
                MemoryProfile.High when name.StartsWith(nameof(Wiki)) => (size + 1) / 2 <= 512,
                MemoryProfile.High => true,
                MemoryProfile.Medium when name.StartsWith(nameof(Wiki)) => (int)Math.Sqrt((size + 1) / 2) + 1 <= 512,
                MemoryProfile.Medium when name.StartsWith(nameof(Grail)) => size <= 512 * 512,
                MemoryProfile.Medium => true,
                _ => false,
            })
                return false;
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

public enum BranchlessProfile
{
    Branchy,
    Branchless,
}
