namespace UMiiSharp;

/// <summary>
/// Maps one enum to Tears of the Kingdom's text and Breath of the Wild's integers. The BotW side
/// is described as a function from an integer to the member name it means, and inverted by
/// trying every integer the game could plausibly store.
/// </summary>
internal sealed class EnumCodec<T> where T : struct, Enum
{
    private readonly Dictionary<string, T> _fromText = new(StringComparer.Ordinal);
    private readonly Dictionary<T, string> _toText = [];
    private readonly Dictionary<int, T> _fromBotw = [];
    private readonly Dictionary<T, int> _toBotw = [];

    public EnumCodec(Func<int, string?> botwName, params (T Value, string Text)[] textOverrides)
    {
        foreach (T value in Enum.GetValues<T>())
        {
            string text = value.ToString();
            foreach (var (overridden, replacement) in textOverrides)
                if (EqualityComparer<T>.Default.Equals(overridden, value)) text = replacement;
            _fromText[text] = value;
            _toText[value] = text;
        }

        for (int code = -1; code <= 300; code++)
        {
            if (botwName(code) is not { } name || !Enum.TryParse(name, ignoreCase: false, out T value)) continue;
            _fromBotw[code] = value;
            _toBotw.TryAdd(value, code);
        }
    }

    public string ToText(T value) => _toText[value];

    public T FromText(string text, string field)
        => _fromText.TryGetValue(text, out T value) ? value
            : throw new InvalidDataException($"{field}: \"{text}\" is not a known {typeof(T).Name}.");

    public int ToBotw(T value, string field)
        => _toBotw.TryGetValue(value, out int code) ? code
            : throw new NotSupportedException($"{field}: {typeof(T).Name}.{value} has no Breath of the Wild equivalent.");

    public T FromBotw(int code, string field)
        => _fromBotw.TryGetValue(code, out T value) ? value
            : throw new InvalidDataException($"{field}: {code} is not a known {typeof(T).Name} code.");
}

internal static class Codecs
{
    // BotW stops at Other; the ancient races, Zonau and Yiga came later.
    public static readonly EnumCodec<Race> Race = new(c => c <= (int)UMiiSharp.Race.Other ? Identity<Race>(c) : null);

    // BotW predates the teenage variants and numbers the rest in its own order.
    public static readonly EnumCodec<SexAge> SexAge = new(c => c switch
    {
        0 => "B", 1 => "M", 2 => "X", 3 => "G", 4 => "W", 5 => "Y", _ => null,
    });

    public static readonly EnumCodec<FfsdType> FfsdType = new(Identity<FfsdType>);

    public static readonly EnumCodec<BodyType> BodyType = new(c => c == -1 ? "None" : c < (int)UMiiSharp.BodyType.None ? Identity<BodyType>(c) : null);

    public static readonly EnumCodec<BodyNumber> BodyNumber = new(c => c is >= 0 and <= 11 ? $"Number{c}" : null);

    public static readonly EnumCodec<BodyWeight> BodyWeight = new(Identity<BodyWeight>);

    public static readonly EnumCodec<BodyHeight> BodyHeight = new(Identity<BodyHeight>, (UMiiSharp.BodyHeight.High, "high"));

    public static readonly EnumCodec<BodyCorrectType> BodyCorrect = new(Identity<BodyCorrectType>);

    public static readonly EnumCodec<Backpack> Backpack = new(c => c == -1 ? "TypeNone" : $"Type{c}");

    public static readonly EnumCodec<Hat> Hat = new(c => c == -1 ? "None" : $"Type{c}");

    public static readonly EnumCodec<FavColor> FavColor = new(c => c == -1 ? "None" : $"Color{c}");

    public static readonly EnumCodec<AccentColor> AccentColor = new(c => c switch
    {
        -1 => "ColorNone",
        >= 0 and <= 11 => $"Color{c}",
        >= 12 and <= 14 => $"SubColor{c}",
        _ => null,
    });

    public static readonly EnumCodec<SubColor> SubColor = new(c => c == -1 ? "SubColorNone" : $"SubColor{c:00}");

    // Korog codes follow TotK's order up to Random; Dynamic is TotK's own. Both games ship
    // exactly nine masks, so BotW's one mask 9 (the backseat Korok) is Random.
    public static readonly EnumCodec<KorogMask> KorogMask
        = new(c => c <= (int)UMiiSharp.KorogMask.Random ? Identity<KorogMask>(c) : null);

    public static readonly EnumCodec<KorogPlant> KorogPlant
        = new(c => c <= (int)UMiiSharp.KorogPlant.Random ? Identity<KorogPlant>(c) : null);

    public static readonly EnumCodec<KorogSkinColor> KorogSkinColor
        = new(c => c <= (int)UMiiSharp.KorogSkinColor.Random ? Identity<KorogSkinColor>(c) : null);

    public static readonly EnumCodec<Personality> Personality = new(_ => null);

    /// <summary>BotW spells personalities with an underscore after the age group: Man_Normal.</summary>
    public static string PersonalityToBotw(Personality value)
    {
        string text = value.ToString();
        for (int i = 1; i < text.Length; i++)
            if (char.IsUpper(text[i])) return string.Concat(text.AsSpan(0, i), "_", text.AsSpan(i));
        return text;
    }

    public static Personality PersonalityFromBotw(string text, string field)
        => Personality.FromText(text.Replace("_", ""), field);

    private static string? Identity<T>(int code) where T : struct, Enum
        => Enum.IsDefined(typeof(T), code) ? ((T)(object)code).ToString() : null;
}
