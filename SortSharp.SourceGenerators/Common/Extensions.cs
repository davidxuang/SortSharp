using OneOf;

namespace SortSharp.SourceGenerators.Common;

internal static class Extensions
{
    extension(HashCode)
    {
        public static int CombineAll<T>(IEnumerable<T> values)
        {
            var hash = new HashCode();
            foreach (var value in values)
                hash.Add(value);
            return hash.ToHashCode();
        }
    }

    extension<T>(IEnumerable<T> values)
    {
        public int FindIndex(Predicate<T> predicate)
        {
            int index = 0;
            foreach (var value in values)
            {
                if (predicate(value))
                    return index;
                index++;
            }
            return -1;
        }

        public void Iterate(Action<T> action)
        {
            foreach (var value in values)
                action(value);
        }
    }

    extension(IEnumerable<string> strings)
    {
        public string Join(string separator = "")
            => string.Join(separator, strings);
    }

    extension<L, R>(OneOf<L, IEnumerable<R>> oneOf)
    {
        public IEnumerable<OneOf<L, R>> Sequence()
            => oneOf.Match(
                left => [oneOf.AsT0],
                rights => rights.Select(right => (OneOf<L, R>)right));
    }

    extension<L, R>(OneOf<IEnumerable<L>, IEnumerable<R>> oneOf)
    {
        public IEnumerable<OneOf<L, R>> Bisequence()
            => oneOf.Match(
                lefts => lefts.Select(left => (OneOf<L, R>)left),
                rights => rights.Select(right => (OneOf<L, R>)right));
    }

    extension<L, R>(IEnumerable<OneOf<L, R>> oneOf)
    {
        public IEnumerable<L> Lefts()
            => oneOf.SelectMany(one => one.Match<IEnumerable<L>>(
                left => [left],
                right => []));

        public IEnumerable<R> Rights()
            => oneOf.SelectMany(one => one.Match<IEnumerable<R>>(
                left => [],
                right => [right]));

        public (IEnumerable<L> Lefts, IEnumerable<R> Rights) Seperate()
        {
            var lefts = new List<L>();
            var rights = new List<R>();
            foreach (var one in oneOf)
                one.Switch(lefts.Add, rights.Add);
            return (lefts, rights);
        }

        public OneOf<IEnumerable<L>, IEnumerable<R>> ValidateAll()
        {
            var lefts = new List<L>();
            var rights = new List<R>();
            foreach (var one in oneOf)
                one.Switch(lefts.Add, rights.Add);
            return lefts.Count > 0 ? lefts : rights;
        }
    }
}
