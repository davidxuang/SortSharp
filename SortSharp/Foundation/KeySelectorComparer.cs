using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SortSharp.Foundation;

internal readonly struct KeySelectorComparer<T, TKey, TSelector> : IComparer<T>
    where TKey : IComparable<TKey>
#if NET7_0_OR_GREATER
    where TSelector : IKeySelector<T, TKey>
#else
    where TSelector : unmanaged, IKeySelector<T, TKey>
#endif
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(T? x, T? y)
    {
#if NET7_0_OR_GREATER
        TKey? k = TSelector.Select(in y);
        return TSelector.Select(in x)?.CompareTo(k) ?? (k is null ? 0 : -1);
#else
        TKey? k = default(TSelector).SelectInst(in y);
        return default(TSelector).SelectInst(in x)?.CompareTo(k!) ?? (k is null ? 0 : -1);
#endif
    }
}

internal readonly struct KeySelectorComparer<T, TKey, TSelector, TComparer>(TComparer comparer)
    : IComparer<T>
#if NET7_0_OR_GREATER
    where TSelector : IKeySelector<T, TKey>
#else
    where TSelector : unmanaged, IKeySelector<T, TKey>
#endif
    where TComparer : IComparer<TKey>
{
    private readonly TComparer _comparer = comparer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(T? x, T? y)
#if NET7_0_OR_GREATER
        => _comparer.Compare(TSelector.Select(in x)!, TSelector.Select(in y)!);
#else
        => _comparer.Compare(default(TSelector).SelectInst(in x)!, default(TSelector).SelectInst(in y)!);
#endif
}
