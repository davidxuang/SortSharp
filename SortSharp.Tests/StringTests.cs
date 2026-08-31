using System.Diagnostics.CodeAnalysis;
using SortSharp.Testing;

namespace SortSharp.Tests;

[MethodDataSource(nameof(GetSettings))]
public sealed class StringTests(int Length, StringPattern Pattern, StringOrder Order)
{
    public static IEnumerable<ValueTuple<int, StringPattern, StringOrder>> GetSettings()
    {
        foreach (var pattern in new[] { StringPattern.Ascii64 })
        {
            foreach (var length in new[] { 2, 3, 4, 16, 4099, 65_537 })
            {
                yield return (length, pattern, StringOrder.Default);
                yield return (length, pattern, StringOrder.Ordinal);
            }
        }
    }

    private string[]? _expected;
    private string[]? _items;

    private Span<string> Items => _items.AsSpan(0, Length);

    [Before(Test)]
    [MemberNotNull(nameof(_expected), nameof(_expected), nameof(_items))]
    public void Setup()
    {
        _expected = new StringGenerator().GetArray(Length, Pattern);
        _items = new string[Length];
        Array.Copy(_expected, _items, Length);

        if (Order == StringOrder.Default)
            Array.Sort(_expected, 0, Length, Comparer<string>.Default);
        else if (Order == StringOrder.Ordinal)
            Array.Sort(_expected, 0, Length, StringComparer.Ordinal);
    }

    private async Task Validate()
    {
        IEqualityComparer<string> comparer = Order switch
        {
            StringOrder.Default => EqualityComparer<string>.Default,
            StringOrder.Ordinal => StringComparer.Ordinal,
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
        if (Order == StringOrder.Default)
            Items.IPNSort();
        else
            Items.IPNSort(StringComparer.Ordinal);

        await Validate();
    }

    [Test]
    public async Task PDQSort()
    {
        if (Order == StringOrder.Default)
            Items.PDQSort();
        else
            Items.PDQSort(StringComparer.Ordinal);
        await Validate();
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.GrailProfiles))]
    public async Task GrailSort(MemoryProfile profile)
    {
        if (Order == StringOrder.Default)
            Items.GrailSort(profile);
        else
            Items.GrailSort(StringComparer.Ordinal, profile);

        await Validate();
    }

    [Test]
    [MethodDataSource(typeof(Common), nameof(Common.WikiProfiles))]
    public async Task WikiSort(MemoryProfile profile)
    {
        if (Order == StringOrder.Default)
            Items.WikiSort(profile);
        else
            Items.WikiSort(StringComparer.Ordinal, profile);

        await Validate();
    }

    [Test, ClassArgumentOnly<StringOrder>(StringOrder.Ordinal)]
    [MethodDataSource(typeof(Common), nameof(Common.RafixMsdProfiles))]
    public async Task RadixMsdSort(MemoryProfile profile)
    {
        Items.RadixMsdSort(profile);

        await Validate();
    }
}
