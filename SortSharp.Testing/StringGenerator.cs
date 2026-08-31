namespace SortSharp.Testing;

public enum StringPattern
{
    Ascii64,
    Ascii64Prefix48,
    Dna32,
    NounZipf1,
}

public readonly struct StringGenerator()
{
    private readonly char[] buffer = new char[64];

    public string[] GetArray(int length, StringPattern pattern)
    {
        var array = new string[length];
        var random = new Random(42);

        switch (pattern)
        {
            case StringPattern.Ascii64:
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < 64; j++)
                        buffer[j] = char.ConvertFromUtf32(random.Next(0x20, 0x7f))[0];
                    array[i] = new string(buffer);
                }
                break;
            case StringPattern.Ascii64Prefix48:
                for (int j = 0; j < 48; j++)
                    buffer[j] = char.ConvertFromUtf32(random.Next(0x20, 0x7f))[0];
                for (int i = 0; i < length; i++)
                {
                    for (int j = 48; j < 64; j++)
                        buffer[j] = char.ConvertFromUtf32(random.Next(0x20, 0x7f))[0];
                    array[i] = new string(buffer);
                }
                break;
            case StringPattern.Dna32:
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < 32; j++)
                        buffer[j] = random.Next(4) switch
                        {
                            0 => 'A',
                            1 => 'C',
                            2 => 'G',
                            3 => 'T',
                            _ => throw new InvalidDataException(),
                        };
                    array[i] = new string(buffer, 0, 32);
                }
                break;
            case StringPattern.NounZipf1:
                var zipf = new ZipfSampler(CocaNoun.List.Length, 1.0);
                for (int i = 0; i < length; i++)
                {
                    long value = zipf.Next(random);
                    array[i] = CocaNoun.List[(int)value].CreateString();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        return array;
    }
}

internal static class CocaNoun
{
    public static readonly string[] List;

    static CocaNoun()
    {
        var assembly = typeof(CocaNoun).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(str => str.EndsWith("coca_lemma_5k_noun.txt"));
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException();
        static IEnumerable<string> read(Stream stream)
        {
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
                yield return line;
        }
        List = [.. read(stream).Where(s => !string.IsNullOrWhiteSpace(s) && char.IsAsciiLetter(s[0]))];
    }
}

public enum StringOrder
{
    Default,
    Ordinal,
}
