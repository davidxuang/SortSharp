namespace SortSharp;

#if NET7_0_OR_GREATER
/// <typeparam name="T">The type of the item to select a key from.</typeparam>
/// <typeparam name="TKey">The type of the key to select.</typeparam>
public interface IKeySelector<T, TKey>
{
    /// <param name="item">The item to select a key from.</param>
    /// <returns>The key selected from the item.</returns>
    static abstract TKey? Select(ref readonly T? item);
}
#else
internal interface IKeySelector<T, TKey>
{
    TKey? SelectInst(ref readonly T? item);
}
#endif
