using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace UMiiSharp.Aamp;

public enum ParameterType : byte
{
    Bool = 0,
    F32 = 1,
    Int = 2,
    Vec2 = 3,
    Vec3 = 4,
    Vec4 = 5,
    Color = 6,
    String32 = 7,
    String64 = 8,
    Curve1 = 9,
    Curve2 = 10,
    Curve3 = 11,
    Curve4 = 12,
    BufferInt = 13,
    BufferF32 = 14,
    String256 = 15,
    Quat = 16,
    U32 = 17,
    BufferU32 = 18,
    BufferBinary = 19,
    StringRef = 20,
}

/// <summary>
/// One AAMP value. Strings are held as text; everything else as the little endian bytes the
/// file stores, so types this library has no use for still round trip untouched.
/// </summary>
public sealed class Parameter
{
    private Parameter(ParameterType type, byte[]? raw, string? text)
    {
        Type = type;
        Raw = raw ?? [];
        Text = text;
    }

    public ParameterType Type { get; }

    /// <summary>The value bytes (element bytes for buffers, without the count). Empty for strings.</summary>
    public byte[] Raw { get; }

    /// <summary>The value for the string types; null otherwise.</summary>
    public string? Text { get; }

    public bool IsString => Type is ParameterType.String32 or ParameterType.String64
        or ParameterType.String256 or ParameterType.StringRef;

    public bool IsBuffer => Type is ParameterType.BufferInt or ParameterType.BufferF32
        or ParameterType.BufferU32 or ParameterType.BufferBinary;

    public static Parameter FromRaw(ParameterType type, byte[] raw)
    {
        if (type is ParameterType.String32 or ParameterType.String64
            or ParameterType.String256 or ParameterType.StringRef)
            throw new ArgumentException("String parameters are built with FromString.", nameof(type));
        return new(type, raw, null);
    }

    public static Parameter FromString(ParameterType type, string value)
    {
        if (type is not (ParameterType.String32 or ParameterType.String64
            or ParameterType.String256 or ParameterType.StringRef))
            throw new ArgumentException($"{type} is not a string type.", nameof(type));
        return new(type, null, value);
    }

    public static Parameter FromBool(bool value) => Word(ParameterType.Bool, value ? 1 : 0);
    public static Parameter FromInt(int value) => Word(ParameterType.Int, value);
    public static Parameter FromUInt(uint value) => Word(ParameterType.U32, unchecked((int)value));
    public static Parameter FromFloat(float value) => Word(ParameterType.F32, BitConverter.SingleToInt32Bits(value));

    public static Parameter FromVector3(Vector3 value)
    {
        byte[] raw = new byte[12];
        BinaryPrimitives.WriteSingleLittleEndian(raw, value.X);
        BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(8), value.Z);
        return new(ParameterType.Vec3, raw, null);
    }

    public bool AsBool() => Expect(ParameterType.Bool) && BinaryPrimitives.ReadUInt32LittleEndian(Raw) != 0;
    public int AsInt() => Expect(ParameterType.Int) ? BinaryPrimitives.ReadInt32LittleEndian(Raw) : 0;
    public uint AsUInt() => Expect(ParameterType.U32) ? BinaryPrimitives.ReadUInt32LittleEndian(Raw) : 0;
    public float AsFloat() => Expect(ParameterType.F32) ? BinaryPrimitives.ReadSingleLittleEndian(Raw) : 0;

    public Vector3 AsVector3() => Expect(ParameterType.Vec3)
        ? new(BinaryPrimitives.ReadSingleLittleEndian(Raw),
              BinaryPrimitives.ReadSingleLittleEndian(Raw.AsSpan(4)),
              BinaryPrimitives.ReadSingleLittleEndian(Raw.AsSpan(8)))
        : default;

    public string AsString() => Text ?? throw new InvalidOperationException($"Parameter is {Type}, not a string.");

    public override string ToString() => IsString ? $"{Type} \"{Text}\"" : $"{Type} ({Raw.Length} bytes)";

    private static Parameter Word(ParameterType type, int value)
    {
        byte[] raw = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(raw, value);
        return new(type, raw, null);
    }

    private bool Expect(ParameterType type)
        => Type == type ? true : throw new InvalidOperationException($"Parameter is {Type}, not {type}.");

    /// <summary>Byte size of a fixed size value type, or -1 for strings and buffers.</summary>
    internal static int FixedSize(ParameterType type) => type switch
    {
        ParameterType.Bool or ParameterType.F32 or ParameterType.Int or ParameterType.U32 => 4,
        ParameterType.Vec2 => 8,
        ParameterType.Vec3 => 12,
        ParameterType.Vec4 or ParameterType.Color or ParameterType.Quat => 16,
        ParameterType.Curve1 => 0x80,
        ParameterType.Curve2 => 0x100,
        ParameterType.Curve3 => 0x180,
        ParameterType.Curve4 => 0x200,
        _ => -1,
    };
}

/// <summary>An ordered set of parameters keyed by the CRC32 of their names.</summary>
public sealed class ParameterObject
{
    public List<KeyValuePair<uint, Parameter>> Parameters { get; } = [];

    public Parameter? this[uint hash]
    {
        get
        {
            foreach (var (key, value) in Parameters)
                if (key == hash) return value;
            return null;
        }
    }

    public Parameter? this[string name] => this[Crc32.Hash(name)];

    public void Set(string name, Parameter value) => Set(Crc32.Hash(name), value);

    public void Set(uint hash, Parameter value)
    {
        for (int i = 0; i < Parameters.Count; i++)
        {
            if (Parameters[i].Key != hash) continue;
            Parameters[i] = new(hash, value);
            return;
        }
        Parameters.Add(new(hash, value));
    }
}

/// <summary>An ordered set of child lists and objects keyed by the CRC32 of their names.</summary>
public sealed class ParameterList
{
    public List<KeyValuePair<uint, ParameterList>> Lists { get; } = [];
    public List<KeyValuePair<uint, ParameterObject>> Objects { get; } = [];

    public ParameterObject? Object(string name) => Object(Crc32.Hash(name));

    public ParameterObject? Object(uint hash)
    {
        foreach (var (key, value) in Objects)
            if (key == hash) return value;
        return null;
    }

    public ParameterList? List(string name) => List(Crc32.Hash(name));

    public ParameterList? List(uint hash)
    {
        foreach (var (key, value) in Lists)
            if (key == hash) return value;
        return null;
    }
}

/// <summary>
/// A version 2 AAMP document, the little endian parameter archive Breath of the Wild uses for
/// actor parameters. Writing lays sections out the way Nintendo's files are laid out (lists,
/// objects, parameters, deduplicated values, deduplicated strings), so unmodified documents
/// come back byte for byte.
/// </summary>
public sealed class ParameterIO
{
    public const string RootName = "param_root";

    private const int HeaderSize = 0x30;
    private const int ListSize = 12;
    private const int ObjectSize = 8;
    private const int ParameterSize = 8;

    public uint Version { get; set; }

    /// <summary>The document type string, "xml" for every file the game ships.</summary>
    public string Type { get; set; } = "xml";

    public ParameterList Root { get; set; } = new();

    /// <summary>The hash of the root list's name; <c>param_root</c> in every shipped file.</summary>
    public uint RootHash { get; set; } = Crc32.Hash(RootName);

    public static ParameterIO FromFile(string path) => FromBinary(File.ReadAllBytes(path));

    // ---- reading ----------------------------------------------------------

    public static ParameterIO FromBinary(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || !data[..4].SequenceEqual("AAMP"u8))
            throw new InvalidDataException("Not an AAMP document: expected an AAMP magic.");

        uint version = U32(data, 0x04);
        if (version != 2)
            throw new InvalidDataException($"Unsupported AAMP version {version}; this reads version 2.");

        uint flags = U32(data, 0x08);
        if ((flags & 1) == 0)
            throw new InvalidDataException("Big endian AAMP (Wii U) is not supported.");

        int pioOffset = checked((int)U32(data, 0x14));
        int typeEnd = data[HeaderSize..].IndexOf((byte)0);

        ParameterIO pio = new()
        {
            Version = U32(data, 0x10),
            Type = Encoding.UTF8.GetString(data.Slice(HeaderSize, Math.Max(typeEnd, 0))),
        };

        int root = HeaderSize + pioOffset;
        pio.RootHash = U32(data, root);
        pio.Root = ReadList(data, root);
        return pio;
    }

    private static ParameterList ReadList(ReadOnlySpan<byte> data, int at)
    {
        ParameterList list = new();

        int lists = at + U16(data, at + 4) * 4;
        int listCount = U16(data, at + 6);
        int objects = at + U16(data, at + 8) * 4;
        int objectCount = U16(data, at + 10);

        for (int i = 0; i < listCount; i++)
        {
            int child = lists + i * ListSize;
            list.Lists.Add(new(U32(data, child), ReadList(data, child)));
        }

        for (int i = 0; i < objectCount; i++)
        {
            int obj = objects + i * ObjectSize;
            list.Objects.Add(new(U32(data, obj), ReadObject(data, obj)));
        }

        return list;
    }

    private static ParameterObject ReadObject(ReadOnlySpan<byte> data, int at)
    {
        ParameterObject obj = new();

        int parameters = at + U16(data, at + 4) * 4;
        int count = U16(data, at + 6);

        for (int i = 0; i < count; i++)
        {
            int p = parameters + i * ParameterSize;
            uint word = U32(data, p + 4);
            int valueAt = p + (int)(word & 0xFFFFFF) * 4;
            var type = (ParameterType)(word >> 24);
            obj.Parameters.Add(new(U32(data, p), ReadValue(data, type, valueAt)));
        }

        return obj;
    }

    private static Parameter ReadValue(ReadOnlySpan<byte> data, ParameterType type, int at)
    {
        switch (type)
        {
            case ParameterType.String32 or ParameterType.String64
                or ParameterType.String256 or ParameterType.StringRef:
                int end = data[at..].IndexOf((byte)0);
                return Parameter.FromString(type, Encoding.UTF8.GetString(data.Slice(at, end < 0 ? data.Length - at : end)));

            case ParameterType.BufferInt or ParameterType.BufferF32 or ParameterType.BufferU32:
                return Parameter.FromRaw(type, data.Slice(at, checked((int)U32(data, at - 4) * 4)).ToArray());

            case ParameterType.BufferBinary:
                return Parameter.FromRaw(type, data.Slice(at, checked((int)U32(data, at - 4))).ToArray());

            default:
                int size = Parameter.FixedSize(type);
                if (size < 0) throw new InvalidDataException($"Unknown AAMP parameter type {(int)type}.");
                return Parameter.FromRaw(type, data.Slice(at, size).ToArray());
        }
    }

    private static ushort U16(ReadOnlySpan<byte> data, int at) => BinaryPrimitives.ReadUInt16LittleEndian(data[at..]);
    private static uint U32(ReadOnlySpan<byte> data, int at) => BinaryPrimitives.ReadUInt32LittleEndian(data[at..]);

    // ---- writing ----------------------------------------------------------

    public void ToFile(string path) => File.WriteAllBytes(path, ToBinary());

    public byte[] ToBinary()
    {
        // Lists go out breadth first so every list's children sit next to each other, then the
        // objects of each list in the same order, then every object's parameters.
        List<(uint Hash, ParameterList List)> lists = [(RootHash, Root)];
        for (int i = 0; i < lists.Count; i++)
            foreach (var (hash, child) in lists[i].List.Lists)
                lists.Add((hash, child));

        List<(uint Hash, ParameterObject Object)> objects = [];
        foreach (var (_, list) in lists)
            foreach (var (hash, obj) in list.Objects)
                objects.Add((hash, obj));

        int parameterCount = objects.Sum(o => o.Object.Parameters.Count);

        byte[] typeBytes = Encoding.UTF8.GetBytes(Type);
        int pioOffset = Align4(typeBytes.Length + 1);

        int listStart = HeaderSize + pioOffset;
        int objectStart = listStart + lists.Count * ListSize;
        int parameterStart = objectStart + objects.Count * ObjectSize;
        int dataStart = parameterStart + parameterCount * ParameterSize;

        // Values, then strings, in first use order. A value reuses any earlier 4-byte aligned run
        // of the same bytes, even one inside a longer value (a float can point into a Vec3), as
        // Nintendo's writer does; strings are reused only whole.
        MemoryStream values = new();
        MemoryStream strings = new();
        Dictionary<string, int> stringOffsets = [];
        List<(int Offset, bool IsString)> placements = new(parameterCount);

        foreach (var (_, obj) in objects)
        {
            foreach (var (_, parameter) in obj.Parameters)
            {
                if (parameter.IsString)
                {
                    string text = parameter.Text!;
                    if (!stringOffsets.TryGetValue(text, out int at))
                    {
                        at = (int)strings.Length;
                        stringOffsets[text] = at;
                        byte[] bytes = Encoding.UTF8.GetBytes(text);
                        strings.Write(bytes);
                        strings.Write(new byte[Align4(bytes.Length + 1) - bytes.Length]);
                    }
                    placements.Add((at, true));
                }
                else
                {
                    byte[] block = parameter.IsBuffer ? WithCount(parameter) : parameter.Raw;
                    int prefix = parameter.IsBuffer ? 4 : 0;
                    int at = FindAligned(values.GetBuffer().AsSpan(0, (int)values.Length), block);
                    if (at < 0)
                    {
                        at = (int)values.Length;
                        values.Write(block);
                        values.Write(new byte[Align4(block.Length) - block.Length]);
                    }
                    placements.Add((at + prefix, false));
                }
            }
        }

        int stringStart = dataStart + (int)values.Length;
        int fileSize = stringStart + (int)strings.Length;
        byte[] output = new byte[fileSize];
        Span<byte> o = output;

        "AAMP"u8.CopyTo(o);
        W32(o, 0x04, 2);
        W32(o, 0x08, 3); // little endian, UTF-8
        W32(o, 0x0C, (uint)fileSize);
        W32(o, 0x10, Version);
        W32(o, 0x14, (uint)pioOffset);
        W32(o, 0x18, (uint)lists.Count);
        W32(o, 0x1C, (uint)objects.Count);
        W32(o, 0x20, (uint)parameterCount);
        W32(o, 0x24, (uint)values.Length);
        W32(o, 0x28, (uint)strings.Length);
        W32(o, 0x2C, 0);
        typeBytes.CopyTo(o[HeaderSize..]);

        int nextList = listStart + ListSize; // the root's children start right after it
        int nextObject = objectStart;
        for (int i = 0; i < lists.Count; i++)
        {
            int at = listStart + i * ListSize;
            ParameterList list = lists[i].List;
            W32(o, at, lists[i].Hash);
            W16(o, at + 4, Units(nextList - at));
            W16(o, at + 6, list.Lists.Count);
            W16(o, at + 8, Units(nextObject - at));
            W16(o, at + 10, list.Objects.Count);
            nextList += list.Lists.Count * ListSize;
            nextObject += list.Objects.Count * ObjectSize;
        }

        int nextParameter = parameterStart;
        int placement = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            int at = objectStart + i * ObjectSize;
            ParameterObject obj = objects[i].Object;
            W32(o, at, objects[i].Hash);
            W16(o, at + 4, Units(nextParameter - at));
            W16(o, at + 6, obj.Parameters.Count);

            foreach (var (hash, parameter) in obj.Parameters)
            {
                var (offset, isString) = placements[placement++];
                int target = (isString ? stringStart : dataStart) + offset;
                int relative = (target - nextParameter) / 4;
                if (relative > 0xFFFFFF) throw new InvalidOperationException("AAMP document too large to address.");

                W32(o, nextParameter, hash);
                W32(o, nextParameter + 4, (uint)relative | (uint)parameter.Type << 24);
                nextParameter += ParameterSize;
            }
        }

        values.ToArray().CopyTo(o[dataStart..]);
        strings.ToArray().CopyTo(o[stringStart..]);
        return output;
    }

    private static byte[] WithCount(Parameter parameter)
    {
        int count = parameter.Type is ParameterType.BufferBinary ? parameter.Raw.Length : parameter.Raw.Length / 4;
        byte[] block = new byte[4 + parameter.Raw.Length];
        BinaryPrimitives.WriteInt32LittleEndian(block, count);
        parameter.Raw.CopyTo(block, 4);
        return block;
    }

    private static int FindAligned(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.IsEmpty) return -1;
        for (int at = 0; at + needle.Length <= haystack.Length; at += 4)
            if (haystack.Slice(at, needle.Length).SequenceEqual(needle)) return at;
        return -1;
    }

    private static int Align4(int value) => (value + 3) & ~3;

    private static int Units(int bytes)
        => bytes / 4 <= ushort.MaxValue ? bytes / 4 : throw new InvalidOperationException("AAMP document too large to address.");

    private static void W16(Span<byte> o, int at, int value) => BinaryPrimitives.WriteUInt16LittleEndian(o[at..], (ushort)value);
    private static void W32(Span<byte> o, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(o[at..], value);
}
