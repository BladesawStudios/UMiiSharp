using BymlSharp;
using UMiiSharp.Aamp;

namespace UMiiSharp;

/// <summary>
/// The look of one UMii character: the <c>Actor/UMii/*.bumii</c> AAMP file in Breath of the Wild
/// actor packs, or the <c>Component/UMiiParam/*.game__component__UMiiParam.bgyml</c> BYML in
/// Tears of the Kingdom ones. Either can be read, edited and written back as either game's file.
/// </summary>
/// <remarks>
/// Converting from TotK to BotW drops what BotW has no place for: the top level
/// <see cref="BodyCorrect"/> block, the hat transform in <see cref="CommonParam"/> and any
/// <see cref="ExtraTotkFields"/>. Values BotW cannot express at all (the teenage
/// <see cref="UMiiSharp.SexAge"/> values, Korog Random/Dynamic, Dynamic colours, races past
/// <see cref="Race.Other"/>) throw <see cref="NotSupportedException"/>. BotW's
/// <see cref="VoiceType"/> lives in a separate VoiceParam component in TotK and is not written.
/// </remarks>
public sealed class UMii
{
    public Race? Race { get; set; }
    public SexAge? SexAge { get; set; }
    public Personality? Personality { get; set; }

    /// <summary>Breath of the Wild only, e.g. <c>Hylia_Man_Young_Normal04</c>.</summary>
    public string? VoiceType { get; set; }

    public FfsdParam? Ffsd { get; set; }
    public BodyParam? Body { get; set; }
    public PersonalParam? Personal { get; set; }
    public CommonParam? Common { get; set; }
    public ShapeParam? Shape { get; set; }
    public HairParam? Hair { get; set; }
    public EyeParam? Eye { get; set; }
    public EyeCtrlParam? EyeCtrl { get; set; }
    public EyebrowParam? Eyebrow { get; set; }
    public NoseParam? Nose { get; set; }
    public MouthParam? Mouth { get; set; }
    public BeardParam? Beard { get; set; }
    public GlassParam? Glass { get; set; }
    public KorogParam? Korog { get; set; }
    public GoronParam? Goron { get; set; }
    public GerudoParam? Gerudo { get; set; }
    public RitoParam? Rito { get; set; }
    public ZoraParam? Zora { get; set; }

    /// <summary>Tears of the Kingdom only.</summary>
    public BodyCorrectParam? BodyCorrect { get; set; }

    /// <summary>Top level TotK keys UMiiSharp does not model (e.g. <c>BlackboardTableRef</c>).</summary>
    public Dictionary<string, Byml> ExtraTotkFields { get; } = new(StringComparer.Ordinal);

    /// <summary>BotW objects UMiiSharp does not model, kept for writing back.</summary>
    public List<KeyValuePair<uint, ParameterObject>> ExtraBotwObjects { get; } = [];

    /// <summary>The object order of the .bumii this came from, so it writes back as it was.</summary>
    internal List<uint>? BotwOrder { get; set; }

    /// <summary>The key order of the UMiiParam this came from, which sets where each section goes.</summary>
    internal List<string>? TotkOrder { get; set; }

    /// <summary>Makes a UMii with every section present and every field unset.</summary>
    public static UMii CreateEmpty() => new()
    {
        Ffsd = new(), Body = new(), Personal = new(), Common = new(), Shape = new(), Hair = new(),
        Eye = new(), EyeCtrl = new(), Eyebrow = new(), Nose = new(), Mouth = new(), Beard = new(),
        Glass = new(), Korog = new(), Goron = new(), Gerudo = new(), Rito = new(), Zora = new(),
        BodyCorrect = new(),
    };

    // ---- reading ----------------------------------------------------------

    /// <summary>Reads either game's file, telling them apart by magic.</summary>
    public static UMii FromBinary(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 4 && data[..4].SequenceEqual("AAMP"u8)) return FromBumii(data);
        if (data.Length >= 2 && (data[..2].SequenceEqual("YB"u8) || data[..2].SequenceEqual("BY"u8)))
            return FromUMiiParam(data);
        throw new InvalidDataException("Not a UMii file: expected an AAMP (.bumii) or BYML (UMiiParam) document.");
    }

    public static UMii FromFile(string path) => FromBinary(File.ReadAllBytes(path));

    /// <summary>Reads a Breath of the Wild <c>.bumii</c>.</summary>
    public static UMii FromBumii(ReadOnlySpan<byte> data) => FromBumii(ParameterIO.FromBinary(data));

    public static UMii FromBumii(ParameterIO pio)
    {
        UMii umii = new();
        BotwReader reader = new(pio);
        umii.Visit(reader);
        reader.Finish(umii);
        return umii;
    }

    /// <summary>Reads a Tears of the Kingdom <c>UMiiParam</c> BYML (already decompressed).</summary>
    public static UMii FromUMiiParam(ReadOnlySpan<byte> data) => FromUMiiParam(Byml.FromBinary(data));

    public static UMii FromUMiiParam(Byml root)
    {
        if (!root.IsMap) throw new InvalidDataException("A UMiiParam document's root must be a map.");
        UMii umii = new();
        TotkReader reader = new(root);
        umii.Visit(reader);
        reader.Finish(umii);
        return umii;
    }

    // ---- writing ----------------------------------------------------------

    /// <summary>
    /// Builds a Breath of the Wild <c>.bumii</c>. Unset fields are left out, as some of the
    /// game's own files do, except personality and voice_type, which are always written ("" when unset).
    /// </summary>
    public ParameterIO ToParameterIO()
    {
        // BotW has nowhere to keep the race but body, or the age, personality and voice but
        // personal, so a TotK UMii that sets them without those blocks gets empty ones.
        BodyParam? body = Body;
        PersonalParam? personal = Personal;
        if (Race is not null) Body ??= new();
        if (SexAge is not null || Personality is not null || VoiceType is not null) Personal ??= new();

        try
        {
            BotwWriter writer = new();
            Visit(writer);
            foreach (var extra in ExtraBotwObjects) writer.Pio.Root.Objects.Add(extra);
            BotwWriter.Arrange(writer.Pio.Root.Objects, BotwOrder);
            return writer.Pio;
        }
        finally
        {
            Body = body;
            Personal = personal;
        }
    }

    public byte[] ToBumii() => ToParameterIO().ToBinary();

    /// <summary>Builds a Tears of the Kingdom <c>UMiiParam</c> document. Unset fields are left out.</summary>
    public Byml ToByml()
    {
        TotkWriter writer = new();
        Visit(writer);
        foreach (var (key, value) in ExtraTotkFields) writer.Root[key] = value;
        return Byml.Map(TotkWriter.Arrange(writer.Root, TotkOrder));
    }

    /// <summary>The uncompressed BYML v7 bytes the game ships.</summary>
    public byte[] ToUMiiParam() => ToByml().ToBinary(BymlVersion.Latest);

    // ---- layout -----------------------------------------------------------

    // Section order is Breath of the Wild's object order; TotK sorts its keys anyway.
    private void Visit(FieldVisitor v)
    {
        Ffsd = v.Section("Ffsd", "ffsd", Ffsd, this);
        Body = v.Section("Body", "body", Body, this);
        Personal = v.Section("Personal", "personal", Personal, this);
        Common = v.Section("Common", "common", Common, this);
        Shape = v.Section("Shape", "shape", Shape, this);
        Hair = v.Section("Hair", "hair", Hair, this);
        Eye = v.Section("Eye", "eye", Eye, this);
        EyeCtrl = v.Section("EyeCtrl", "eye_ctrl", EyeCtrl, this);
        Eyebrow = v.Section("Eyebrow", "eyebrow", Eyebrow, this);
        Nose = v.Section("Nose", "nose", Nose, this);
        Mouth = v.Section("Mouth", "mouth", Mouth, this);
        Beard = v.Section("Beard", "beard", Beard, this);
        Glass = v.Section("Glass", "glass", Glass, this);
        Korog = v.Section("Korog", "korog", Korog, this);
        Goron = v.Section("Goron", "goron", Goron, this);
        Gerudo = v.Section("Gerudo", "gerudo", Gerudo, this);
        Rito = v.Section("Rito", "rito", Rito, this);
        Zora = v.Section("Zora", "zora", Zora, this);
        BodyCorrect = v.Section("BodyCorrect", null, BodyCorrect, this);

        // BotW keeps these inside Body and Personal; TotK has them at the top level.
        Race = v.Enum("Race", null, Race, Codecs.Race);
        SexAge = v.Enum("SexAge", null, SexAge, Codecs.SexAge);
        Personality = v.Enum("Personality", null, Personality, Codecs.Personality);
    }
}
