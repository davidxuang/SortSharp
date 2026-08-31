using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using SortSharp.Compat;
using SortSharp.Testing;

namespace SortSharp.Tests;

[MethodDataSource(nameof(GetSettings))]
public sealed class DoubleTests(int Length, DoublePattern Pattern, BfpIeeeOrder Order)
{
    public static IEnumerable<ValueTuple<int, DoublePattern, BfpIeeeOrder>> GetSettings()
    {
        foreach (var pattern in new[] { DoublePattern.RandomCategory })
        {
            foreach (var length in new[] { 2, 3, 4, 16, 4099, 65_537 })
            {
                yield return (length, pattern, BfpIeeeOrder.Default);
                yield return (length, pattern, BfpIeeeOrder.TotalIeee754);
            }
        }
    }

    private double[]? _expected;
    private double[]? _items;

    private Span<double> Items => _items.AsSpan(0, Length);

    [Before(Test)]
    [MemberNotNull(nameof(_expected), nameof(_expected), nameof(_items))]
    public void Setup()
    {
        _expected = new DoubleGenerator().GetArray(Length, Pattern);
        _items = new double[Length];
        Array.Copy(_expected, _items, Length);

        if (Order == BfpIeeeOrder.Default)
            Array.Sort(_expected, 0, Length, Comparer<double>.Default);
        else if (Order == BfpIeeeOrder.TotalIeee754)
            Array.Sort(_expected, 0, Length, new TotalOrderIeee754Comparer<double>());
    }

    private async Task Validate()
    {
        IEqualityComparer<double> comparer = Order switch
        {
            BfpIeeeOrder.Default => EqualityComparer<double>.Default,
            BfpIeeeOrder.TotalIeee754 => new TotalOrderIeee754Comparer<double>(),
            _ => throw new NotSupportedException()
        };

        for (int i = 0; i < Length; i++)
        {
            await Assert.That(_items![i]).IsEqualTo(_expected![i], comparer).Because($"items should be sorted at index {i}");
        }
    }

    [Test]
    public async Task IPNSort()
    {
        if (Order == BfpIeeeOrder.Default)
            Items.IPNSort();
        else
            Items.IPNSort(new TotalOrderIeee754Comparer<double>());

        await Validate();
    }

    [Test]
    public async Task PDQSort()
    {
        if (Order == BfpIeeeOrder.Default)
            Items.PDQSort();
        else
            Items.PDQSort(new TotalOrderIeee754Comparer<double>());
        await Validate();
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.GrailProfiles))]
    public async Task GrailSort(MemoryProfile profile)
    {
        if (Order == BfpIeeeOrder.Default)
            Items.GrailSort(profile);
        else
            Items.GrailSort(new TotalOrderIeee754Comparer<double>(), profile);

        await Validate();
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.WikiProfiles))]
    public async Task WikiSort(MemoryProfile profile)
    {
        if (Order == BfpIeeeOrder.Default)
            Items.WikiSort(profile);
        else
            Items.WikiSort(new TotalOrderIeee754Comparer<double>(), profile);

        await Validate();
    }

    [Test, ClassArgumentOnly<BfpIeeeOrder>(BfpIeeeOrder.TotalIeee754)]
    public async Task RadixLsdSort()
    {
        Items.RadixLsdSort();

        await Validate();
    }

    [Test, ClassArgumentOnly<BfpIeeeOrder>(BfpIeeeOrder.TotalIeee754)]
    [MethodDataSource(typeof(Common), nameof(Common.RafixMsdProfiles))]
    public async Task RadixMsdSort(MemoryProfile profile)
    {
        Items.RadixMsdSort(profile);

        await Validate();
    }
}
