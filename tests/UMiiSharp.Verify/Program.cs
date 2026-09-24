using System.Buffers.Binary;
using BymlSharp;
using SarcSharp;
using UMiiSharp;
using UMiiSharp.Aamp;
using ZsDicSharp;

// Round trips every UMii in both games:
//   BotW  .bumii     -> UMii -> .bumii     (byte for byte), and through TotK and back
//   TotK  UMiiParam  -> UMii -> UMiiParam  (byte for byte, else structurally), and into BotW
//
// usage: UMiiSharp.Verify --botw <romfs> --totk <romfs>

string? botwRomfs = Arg("--botw");
string? totkRomfs = Arg("--totk");
if (botwRomfs is null && totkRomfs is null)
{
    Console.Error.WriteLine("usage: UMiiSharp.Verify --botw <romfs> --totk <romfs>");
    return 2;
}

int failures = 0;
if (botwRomfs is not null) failures += VerifyBotw(botwRomfs);
if (totkRomfs is not null) failures += VerifyTotk(totkRomfs);
Console.WriteLine(failures == 0 ? "all good" : $"{failures} failure(s)");
return failures == 0 ? 0 : 1;

int VerifyBotw(string romfs)
{
    List<(string Name, byte[] Data)> files = [];
    foreach (string path in Directory.EnumerateFiles(Path.Combine(romfs, "Actor", "Pack"), "*.sbactorpack"))
        CollectBumii(File.ReadAllBytes(path), Path.GetFileName(path), files);
    foreach (string path in Directory.EnumerateFiles(Path.Combine(romfs, "Pack"), "*.pack"))
        CollectBumii(File.ReadAllBytes(path), Path.GetFileName(path), files);

    int aampExact = 0, umiiExact = 0, crossExact = 0, bad = 0;
    foreach (var (name, data) in files)
    {
        try
        {
            if (ParameterIO.FromBinary(data).ToBinary().AsSpan().SequenceEqual(data)) aampExact++;
            else Report(name, "AAMP rewrite differs");

            UMii umii = UMii.FromBumii(data);
            if (umii.ToBumii().AsSpan().SequenceEqual(data)) umiiExact++;
            else Report(name, $"UMii rewrite differs{FirstDifference(umii.ToBumii(), data)}");

            // TotK has no layout to carry, so this compares values rather than bytes.
            UMii viaTotk = UMii.FromUMiiParam(umii.ToUMiiParam());
            viaTotk.VoiceType = umii.VoiceType; // TotK keeps voices in a separate component
            UMii back = UMii.FromBumii(viaTotk.ToBumii());
            if (Same(back.ToByml(), umii.ToByml()) && back.VoiceType == umii.VoiceType) crossExact++;
            else Report(name, "BotW -> TotK -> BotW changes a value");
        }
        catch (Exception e)
        {
            Report(name, $"{e.GetType().Name}: {e.Message}");
        }
    }

    Console.WriteLine($"BotW: {files.Count} bumii | AAMP exact {aampExact} | UMii exact {umiiExact} | via TotK unchanged {crossExact}");
    return bad;

    void Report(string name, string message)
    {
        bad++;
        if (bad <= 20) Console.WriteLine($"  BotW {name}: {message}");
    }
}

int VerifyTotk(string romfs)
{
    using ZsDic zs = ZsDic.FromRomfs(romfs);
    List<(string Name, byte[] Data)> files = [];
    foreach (string path in Directory.EnumerateFiles(Path.Combine(romfs, "Pack", "Actor"), "*.pack.zs"))
    {
        byte[] pack = zs.DecompressFile(path);
        if (pack.AsSpan().IndexOf("UMiiParam"u8) < 0) continue;
        foreach (SarcEntry entry in SarcFile.FromBinary(pack).Entries)
            if (entry.Name.Contains("UMiiParam")) files.Add(($"{Path.GetFileName(path)}/{entry.Name}", entry.Data));
    }

    int bymlExact = 0, exact = 0, structural = 0, toBotw = 0, notInBotw = 0, bad = 0;
    Dictionary<string, int> reasons = [];
    foreach (var (name, data) in files)
    {
        try
        {
            if (Byml.FromBinary(data).ToBinary().AsSpan().SequenceEqual(data)) bymlExact++;

            UMii umii = UMii.FromUMiiParam(data);
            byte[] rewritten = umii.ToUMiiParam();
            if (rewritten.AsSpan().SequenceEqual(data)) exact++;
            else if (Same(Byml.FromBinary(rewritten), Byml.FromBinary(data))) structural++;
            else Report(name, "rewrite changes the document");

            try
            {
                UMii back = UMii.FromBumii(umii.ToBumii());
                toBotw++;

                // Everything BotW has a place for must survive the trip; strip what it doesn't.
                UMii expected = UMii.FromUMiiParam(data);
                expected.BodyCorrect = null;
                expected.ExtraTotkFields.Clear();
                if (expected.Common is { } common) common.HatTrans = common.HatScale = null;
                if (expected.Common is { } c) c.HatZRotate = null;
                foreach (var section in new UMiiSection?[] { expected.Ffsd, expected.Body, expected.Personal, expected.Common,
                             expected.Shape, expected.Hair, expected.Eye, expected.EyeCtrl, expected.Eyebrow, expected.Nose,
                             expected.Mouth, expected.Beard, expected.Glass, expected.Korog, expected.Goron,
                             expected.Gerudo, expected.Rito, expected.Zora })
                    section?.ExtraTotkFields.Clear();

                if (!Same(WithoutEmpty(back.ToByml()), WithoutEmpty(expected.ToByml())))
                    Report(name, "TotK -> BotW -> TotK changes the document");
            }
            catch (NotSupportedException e)
            {
                notInBotw++;
                string reason = e.Message[(e.Message.IndexOf(':') + 2)..];
                reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
            }
        }
        catch (Exception e)
        {
            Report(name, $"{e.GetType().Name}: {e.Message}");
        }
    }

    Console.WriteLine($"TotK: {files.Count} UMiiParam | BymlSharp alone exact {bymlExact} | exact {exact} | structurally equal {structural} | " +
                      $"writable as BotW {toBotw} | not expressible in BotW {notInBotw}");
    foreach (var (reason, count) in reasons.OrderByDescending(r => r.Value))
        Console.WriteLine($"    {count,4}  {reason}");
    return bad;

    void Report(string name, string message)
    {
        bad++;
        if (bad <= 20) Console.WriteLine($"  TotK {name}: {message}");
    }
}

void CollectBumii(byte[] data, string where, List<(string, byte[])> into)
{
    if (data.AsSpan().StartsWith("Yaz0"u8)) data = Yaz0(data);
    if (!data.AsSpan().StartsWith("SARC"u8)) return;
    foreach (SarcEntry entry in SarcFile.FromBinary(data).Entries)
    {
        if (entry.Name.EndsWith(".bumii")) into.Add(($"{where}/{entry.Name}", entry.Data));
        else if (entry.Name.EndsWith("pack") || entry.Name.EndsWith("sarc")) CollectBumii(entry.Data, $"{where}/{entry.Name}", into);
    }
}

static byte[] Yaz0(byte[] src)
{
    byte[] dst = new byte[BinaryPrimitives.ReadUInt32BigEndian(src.AsSpan(4))];
    int s = 16, d = 0;
    while (d < dst.Length)
    {
        byte header = src[s++];
        for (int bit = 7; bit >= 0 && d < dst.Length; bit--)
        {
            if ((header >> bit & 1) != 0)
            {
                dst[d++] = src[s++];
                continue;
            }

            int b1 = src[s++], b2 = src[s++];
            int from = d - ((b1 & 0x0F) << 8 | b2) - 1;
            int length = b1 >> 4 == 0 ? src[s++] + 0x12 : (b1 >> 4) + 2;
            for (int i = 0; i < length; i++) dst[d++] = dst[from + i];
        }
    }
    return dst;
}

static bool Same(Byml a, Byml b)
{
    if (a.IsMap && b.IsMap)
        return a.Count == b.Count && a.AsMap.All(kv => b[kv.Key] is { } other && Same(kv.Value, other));
    if (a.IsArray && b.IsArray)
        return a.Count == b.Count && a.AsArray.Zip(b.AsArray).All(p => Same(p.First, p.Second));
    return a.Type == b.Type && a.ToString() == b.ToString();
}

// A BotW trip can add an empty Body or Personal to hold the race or age; that's no change.
static Byml WithoutEmpty(Byml root)
    => Byml.Map(root.AsMap.Where(kv => !(kv.Value.IsMap && kv.Value.Count == 0))
        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal));

static string FirstDifference(byte[] a, byte[] b)
{
    int n = Math.Min(a.Length, b.Length);
    for (int i = 0; i < n; i++)
        if (a[i] != b[i]) return $" at 0x{i:X} (sizes {a.Length} vs {b.Length})";
    return $" in length ({a.Length} vs {b.Length})";
}

string? Arg(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
