using SortSharp.Testing;

namespace SortSharp.Tests;

internal static class Common
{
    internal static IEnumerable<BranchlessProfile> BranchlessProfiles { get; } = [BranchlessProfile.Branchless, BranchlessProfile.Branchy];

    internal static IEnumerable<MemoryProfile> WikiProfiles { get; } = [MemoryProfile.Minimum, MemoryProfile.Baseline, MemoryProfile.Medium, MemoryProfile.High];
    internal static IEnumerable<MemoryProfile> GrailProfiles { get; } = [MemoryProfile.Minimum, MemoryProfile.Baseline, MemoryProfile.Medium];
    internal static IEnumerable<MemoryProfile> RafixMsdProfiles { get; } = [MemoryProfile.Minimum, MemoryProfile.Baseline, MemoryProfile.High];

    internal static IEnumerable<BfpIeeeOrder> IeeeBfpOrders { get; } = [BfpIeeeOrder.Default, BfpIeeeOrder.TotalIeee754];
    internal static IEnumerable<StringOrder> StringOrders { get; } = [StringOrder.Default, StringOrder.Ordinal];
}

public class ClassArgumentOnlyAttribute<T>(T value) : SkipAttribute("The order is not applicable for Radix sorts")
{
    public override async Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return context.TestDetails.TestClassArguments.OfType<T>()
            .Any(order => !EqualityComparer<T>.Default.Equals(order, value));
    }
}
