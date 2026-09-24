using System.Numerics;
using AampSharp;
using BymlSharp;

namespace UMiiSharp;

/// <summary>
/// Walks a UMii's fields, each named for both games. Readers return the value found in the file
/// (null when absent); writers store the value and hand it back. A field with no name in the
/// visitor's game is passed through untouched.
/// </summary>
internal abstract class FieldVisitor
{
    public abstract T? Section<T>(string totk, string? botw, T? current, UMii owner) where T : UMiiSection, new();

    public abstract int? Int(string? totk, string? botw, int? value);
    public abstract float? Float(string? totk, string? botw, float? value);
    public abstract bool? Bool(string? totk, string? botw, bool? value);
    public abstract Vector3? Vector3(string? totk, string? botw, Vector3? value);

    public abstract T? Enum<T>(string? totk, string? botw, T? value, EnumCodec<T> codec) where T : struct, Enum;

    /// <summary>A BotW string that has no TotK counterpart.</summary>
    public abstract string? BotwString(string botw, string? value);

    /// <summary>BotW's personality, a string such as <c>Man_Normal</c>; empty means none.</summary>
    public abstract Personality? BotwPersonality(string botw, Personality? value);
}

// ---- Breath of the Wild ---------------------------------------------------

internal sealed class BotwReader(ParameterIO pio) : FieldVisitor
{
    private readonly HashSet<uint> _usedObjects = [];
    private ParameterObject? _object;
    private HashSet<uint> _usedParameters = [];
    private string _where = "";

    public override T? Section<T>(string totk, string? botw, T? current, UMii owner) where T : class
    {
        if (botw is null) return current;

        uint hash = Crc32.Hash(botw);
        if (pio.Root.Object(hash) is not { } obj) return null;
        _usedObjects.Add(hash);

        T section = new() { BotwOrder = [.. obj.Parameters.Select(p => p.Key)] };
        _object = obj;
        _usedParameters = [];
        _where = botw;
        section.Visit(this, owner);

        foreach (var parameter in obj.Parameters)
            if (!_usedParameters.Contains(parameter.Key)) section.ExtraBotwParameters.Add(parameter);

        _object = null;
        return section;
    }

    public void Finish(UMii umii)
    {
        umii.BotwOrder = [.. pio.Root.Objects.Select(o => o.Key)];
        foreach (var obj in pio.Root.Objects)
            if (!_usedObjects.Contains(obj.Key)) umii.ExtraBotwObjects.Add(obj);
    }

    public override int? Int(string? totk, string? botw, int? value)
        => botw is null ? value : Find(botw)?.AsInt();

    public override float? Float(string? totk, string? botw, float? value)
        => botw is null ? value : Find(botw)?.AsFloat();

    public override bool? Bool(string? totk, string? botw, bool? value)
        => botw is null ? value : Find(botw)?.AsBool();

    public override Vector3? Vector3(string? totk, string? botw, Vector3? value)
        => botw is null ? value : Find(botw)?.AsVector3();

    public override T? Enum<T>(string? totk, string? botw, T? value, EnumCodec<T> codec)
    {
        if (botw is null) return value;
        return Find(botw) is { } p ? codec.FromBotw(p.AsInt(), $"{_where}.{botw}") : null;
    }

    public override string? BotwString(string botw, string? value) => Find(botw)?.AsString();

    public override Personality? BotwPersonality(string botw, Personality? value)
        => Find(botw)?.AsString() is { Length: > 0 } text ? Codecs.PersonalityFromBotw(text, $"{_where}.{botw}") : null;

    private Parameter? Find(string name)
    {
        if (_object is null) return null;
        uint hash = Crc32.Hash(name);
        _usedParameters.Add(hash);
        return _object[hash];
    }
}

internal sealed class BotwWriter : FieldVisitor
{
    private ParameterObject? _object;
    private string _where = "";

    public ParameterIO Pio { get; } = new();

    public override T? Section<T>(string totk, string? botw, T? current, UMii owner) where T : class
    {
        if (botw is null || current is null) return current;

        _object = new();
        _where = botw;
        current.Visit(this, owner);
        foreach (var extra in current.ExtraBotwParameters) _object.Parameters.Add(extra);
        Arrange(_object.Parameters, current.BotwOrder);
        Pio.Root.Objects.Add(new(Crc32.Hash(botw), _object));
        _object = null;
        return current;
    }

    public override int? Int(string? totk, string? botw, int? value)
        => Put(botw, value, v => Parameter.FromInt(v));

    public override float? Float(string? totk, string? botw, float? value)
        => Put(botw, value, v => Parameter.FromFloat(v));

    public override bool? Bool(string? totk, string? botw, bool? value)
        => Put(botw, value, v => Parameter.FromBool(v));

    public override Vector3? Vector3(string? totk, string? botw, Vector3? value)
        => Put(botw, value, v => Parameter.FromVector3(v));

    public override T? Enum<T>(string? totk, string? botw, T? value, EnumCodec<T> codec)
        => Put(botw, value, v => Parameter.FromInt(codec.ToBotw(v, $"{_where}.{botw}")));

    // Every shipped .bumii has both strings, with "" meaning none, so they are always written.
    public override string? BotwString(string botw, string? value)
    {
        Add(botw, Parameter.FromString(ParameterType.StringRef, value ?? ""));
        return value;
    }

    public override Personality? BotwPersonality(string botw, Personality? value)
    {
        Add(botw, Parameter.FromString(ParameterType.StringRef, value is { } v ? Codecs.PersonalityToBotw(v) : ""));
        return value;
    }

    // Other unset fields are left out, as some of BotW's own files do; the game falls back to defaults.
    private TValue? Put<TValue>(string? botw, TValue? value, Func<TValue, Parameter> build) where TValue : struct
    {
        if (botw is not null && value is { } v) Add(botw, build(v));
        return value;
    }

    /// <summary>
    /// Puts entries back in a source file's order. Entries it didn't have keep their canonical
    /// order after the rest.
    /// </summary>
    public static void Arrange<T>(List<KeyValuePair<uint, T>> entries, List<uint>? order)
    {
        if (order is null) return;
        Dictionary<uint, int> rank = [];
        for (int i = 0; i < order.Count; i++) rank.TryAdd(order[i], i);

        var sorted = entries
            .Select((entry, index) => (entry, key: rank.TryGetValue(entry.Key, out int r) ? r : order.Count + index))
            .OrderBy(x => x.key)
            .Select(x => x.entry)
            .ToList();
        entries.Clear();
        entries.AddRange(sorted);
    }

    // Fields outside a section (TotK's top level) have no BotW name and never get here with one.
    private void Add(string botw, Parameter parameter) => _object?.Parameters.Add(new(Crc32.Hash(botw), parameter));
}

// ---- Tears of the Kingdom -------------------------------------------------

internal sealed class TotkReader : FieldVisitor
{
    private readonly IDictionary<string, Byml> _root;
    private readonly HashSet<string> _usedRoot = new(StringComparer.Ordinal);
    private IDictionary<string, Byml> _map;
    private HashSet<string> _used;
    private string _where = "";

    public TotkReader(Byml root)
    {
        _root = root.AsMap;
        _map = _root;
        _used = _usedRoot;
    }

    public override T? Section<T>(string totk, string? botw, T? current, UMii owner) where T : class
    {
        if (!_root.TryGetValue(totk, out Byml? node)) return null;
        if (!node.IsMap) throw new InvalidDataException($"{totk}: expected a map, found {node.Type}.");
        _usedRoot.Add(totk);

        T section = new() { TotkOrder = [.. node.AsMap.Keys] };
        _map = node.AsMap;
        _used = new(StringComparer.Ordinal);
        _where = totk;
        section.Visit(this, owner);

        foreach (var (key, value) in _map)
            if (!_used.Contains(key)) section.ExtraTotkFields[key] = value;

        _map = _root;
        _used = _usedRoot;
        _where = "";
        return section;
    }

    public void Finish(UMii umii)
    {
        umii.TotkOrder = [.. _root.Keys];
        foreach (var (key, value) in _root)
            if (!_usedRoot.Contains(key)) umii.ExtraTotkFields[key] = value;
    }

    public override int? Int(string? totk, string? botw, int? value)
        => totk is null ? value : Find(totk) is { } n ? n.Type is BymlType.Int ? n.Int : (int)Number(n, totk) : null;

    public override float? Float(string? totk, string? botw, float? value)
        => totk is null ? value : Find(totk) is { } n ? n.Type is BymlType.Float ? n.Float : (float)Number(n, totk) : null;

    public override bool? Bool(string? totk, string? botw, bool? value)
        => totk is null ? value : Find(totk) is { } n ? n.Type is BymlType.Bool ? n.Bool : Number(n, totk) != 0 : null;

    public override Vector3? Vector3(string? totk, string? botw, Vector3? value)
    {
        if (totk is null) return value;
        if (Find(totk) is not { } n) return null;
        if (!n.IsMap) throw new InvalidDataException($"{Path(totk)}: expected an X/Y/Z map, found {n.Type}.");
        return new(Axis(n, "X", totk), Axis(n, "Y", totk), Axis(n, "Z", totk));
    }

    public override T? Enum<T>(string? totk, string? botw, T? value, EnumCodec<T> codec)
    {
        if (totk is null) return value;
        if (Find(totk) is not { } n) return null;
        return codec.FromText(n.AsString() ?? throw new InvalidDataException($"{Path(totk)}: expected a string, found {n.Type}."), Path(totk));
    }

    public override string? BotwString(string botw, string? value) => value;

    public override Personality? BotwPersonality(string botw, Personality? value) => value;

    private Byml? Find(string key)
    {
        _used.Add(key);
        return _map.TryGetValue(key, out Byml? node) ? node : null;
    }

    private float Axis(Byml map, string axis, string field)
        => map[axis] is { } n ? (float)Number(n, $"{field}.{axis}") : 0f;

    private double Number(Byml node, string field)
        => node.AsNumber() ?? throw new InvalidDataException($"{Path(field)}: expected a number, found {node.Type}.");

    private string Path(string key) => _where.Length > 0 ? $"{_where}.{key}" : key;
}

internal sealed class TotkWriter : FieldVisitor
{
    private Dictionary<string, Byml> _map;

    public TotkWriter() => _map = Root;

    public Dictionary<string, Byml> Root { get; } = new(StringComparer.Ordinal);

    public override T? Section<T>(string totk, string? botw, T? current, UMii owner) where T : class
    {
        if (current is null) return null;

        _map = new(StringComparer.Ordinal);
        current.Visit(this, owner);
        foreach (var (key, value) in current.ExtraTotkFields) _map[key] = value;
        Root[totk] = Byml.Map(Arrange(_map, current.TotkOrder));
        _map = Root;
        return current;
    }

    /// <summary>
    /// Orders a map's keys for writing. BymlSharp lays nested maps out in this order, so keys
    /// keep the source file's order; anything else goes in the case-insensitive order
    /// Nintendo's own UMiiParam files use.
    /// </summary>
    public static Dictionary<string, Byml> Arrange(Dictionary<string, Byml> map, List<string>? order)
    {
        Dictionary<string, int> rank = [];
        if (order is not null)
            for (int i = 0; i < order.Count; i++) rank.TryAdd(order[i], i);

        return map
            .OrderBy(e => rank.TryGetValue(e.Key, out int r) ? r : int.MaxValue)
            .ThenBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
    }

    public override int? Int(string? totk, string? botw, int? value)
        => Put(totk, value, v => Byml.From(v));

    public override float? Float(string? totk, string? botw, float? value)
        => Put(totk, value, v => Byml.From(v));

    public override bool? Bool(string? totk, string? botw, bool? value)
        => Put(totk, value, v => Byml.From(v));

    public override Vector3? Vector3(string? totk, string? botw, Vector3? value)
        => Put(totk, value, v => Byml.Map(new Dictionary<string, Byml>(StringComparer.Ordinal)
        {
            ["X"] = v.X, ["Y"] = v.Y, ["Z"] = v.Z,
        }));

    public override T? Enum<T>(string? totk, string? botw, T? value, EnumCodec<T> codec)
        => Put(totk, value, v => Byml.From(codec.ToText(v)));

    public override string? BotwString(string botw, string? value) => value;

    public override Personality? BotwPersonality(string botw, Personality? value) => value;

    private TValue? Put<TValue>(string? totk, TValue? value, Func<TValue, Byml> build) where TValue : struct
    {
        if (totk is not null && value is { } v) _map[totk] = build(v);
        return value;
    }
}
