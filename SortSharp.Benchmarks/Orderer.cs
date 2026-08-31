using System.Collections.Immutable;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace SortSharp.Benchmarks;

internal partial class Orderer : IOrderer
{
    private static readonly IOrderer Default = DefaultOrderer.Instance;
    [GeneratedRegex(@",\s*(?:Profile|Width)\s*=\s*\w+")]
    private static partial Regex Re { get; }

    public bool SeparateLogicalGroups => Default.SeparateLogicalGroups;

    public IEnumerable<BenchmarkCase> GetExecutionOrder(
        ImmutableArray<BenchmarkCase> benchmarksCase,
        IEnumerable<BenchmarkLogicalGroupRule> order = null)
        => Default.GetExecutionOrder(benchmarksCase, order);

    public string GetHighlightGroupKey(BenchmarkCase benchmarkCase)
        => Re.Replace(Default.GetHighlightGroupKey(benchmarkCase), string.Empty);

    public string GetLogicalGroupKey(
        ImmutableArray<BenchmarkCase> allBenchmarksCases,
        BenchmarkCase benchmarkCase)
        => Re.Replace(Default.GetLogicalGroupKey(allBenchmarksCases, benchmarkCase), string.Empty);

    public IEnumerable<IGrouping<string, BenchmarkCase>> GetLogicalGroupOrder(
        IEnumerable<IGrouping<string, BenchmarkCase>> logicalGroups,
        IEnumerable<BenchmarkLogicalGroupRule> order = null)
        => Default.GetLogicalGroupOrder(logicalGroups, order);

    public IEnumerable<BenchmarkCase> GetSummaryOrder(
        ImmutableArray<BenchmarkCase> benchmarksCases,
        Summary summary)
    {
        var groups = benchmarksCases.GroupBy(b => GetLogicalGroupKey(benchmarksCases, b));
        foreach (var group in GetLogicalGroupOrder(groups, benchmarksCases.FirstOrDefault()?.Config.GetLogicalGroupRules()))
            foreach (var benchmark in group
                .OrderByDescending(bm => bm.Descriptor.Baseline)
                .ThenBy(bm => bm.Descriptor.WorkloadMethodDisplayInfo)
                .ThenBy(bm => Convert.ToInt64(bm.Parameters["Profile"] ?? 0))
                .ThenBy(bm => Convert.ToInt64(bm.Parameters["Width"] ?? 0)))
                yield return benchmark;
    }
}
