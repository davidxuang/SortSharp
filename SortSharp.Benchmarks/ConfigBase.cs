using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace SortSharp.Benchmarks;

internal class ConfigBase : ManualConfig
{
    public ConfigBase()
    {
        Orderer = new Orderer();
        AddJob(Job.Default
            .WithEnvironmentVariable("DOTNET_TieredPgo", "1"));
        // WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}

public enum BranchlessVariant
{
    Branchy,
    Branchless,
}
