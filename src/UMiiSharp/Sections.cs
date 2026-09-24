using System.Numerics;
using AampSharp;
using BymlSharp;

namespace UMiiSharp;

// Every field is nullable: Tears of the Kingdom files leave out whatever matches the game's
// default, and a null here means "not in the file". Each section lists its fields once, in the
// order Breath of the Wild stores them, naming the key in both games (TotK, then BotW); a null
// name means the other game has no such field.

public abstract class UMiiSection
{
    /// <summary>TotK keys in this section that UMiiSharp does not model, kept for writing back.</summary>
    public Dictionary<string, Byml> ExtraTotkFields { get; } = new(StringComparer.Ordinal);

    /// <summary>BotW parameters in this object that UMiiSharp does not model, kept for writing back.</summary>
    public List<KeyValuePair<uint, Parameter>> ExtraBotwParameters { get; } = [];

    /// <summary>The parameter order of the .bumii this came from; BotW's files disagree on it.</summary>
    internal List<uint>? BotwOrder { get; set; }

    /// <summary>The key order of the UMiiParam this came from, which sets where nested maps go.</summary>
    internal List<string>? TotkOrder { get; set; }

    internal abstract void Visit(FieldVisitor v, UMii owner);
}

public sealed class FfsdParam : UMiiSection
{
    public bool? NoUseFfsd { get; set; }
    public FfsdType? Type { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        NoUseFfsd = v.Bool("NoUseFfsd", "no_use_ffsd", NoUseFfsd);
        Type = v.Enum("Type", "type", Type, Codecs.FfsdType);
    }
}

public sealed class BodyParam : UMiiSection
{
    public BodyType? Type { get; set; }
    public BodyNumber? Number { get; set; }
    public BodyWeight? Weight { get; set; }
    public BodyHeight? Height { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        owner.Race = v.Enum(null, "race", owner.Race, Codecs.Race);
        Type = v.Enum("Type", "type", Type, Codecs.BodyType);
        Number = v.Enum("Number", "number", Number, Codecs.BodyNumber);
        Weight = v.Enum("Weight", "weight", Weight, Codecs.BodyWeight);
        Height = v.Enum("Height", "height", Height, Codecs.BodyHeight);
    }
}

public sealed class PersonalParam : UMiiSection
{
    public FavColor? FavColor { get; set; }
    public SubColor? SubColor1 { get; set; }
    public SubColor? SubColor2 { get; set; }
    public AccentColor? HeadFavColor { get; set; }
    /// <summary>Also the backpack colour for Koroks.</summary>
    public AccentColor? ShoulderFavColor { get; set; }
    public AccentColor? ShoulderSubColor1 { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        owner.SexAge = v.Enum(null, "sex_age", owner.SexAge, Codecs.SexAge);
        FavColor = v.Enum("FavColor", "fav_color", FavColor, Codecs.FavColor);
        SubColor1 = v.Enum("SubColor1", "sub_color_1", SubColor1, Codecs.SubColor);
        SubColor2 = v.Enum("SubColor2", "sub_color_2", SubColor2, Codecs.SubColor);
        HeadFavColor = v.Enum("HeadFavColor", "head_fav_color", HeadFavColor, Codecs.AccentColor);
        ShoulderFavColor = v.Enum("ShoulderFavColor", "shoulder_fav_color", ShoulderFavColor, Codecs.AccentColor);
        ShoulderSubColor1 = v.Enum("ShoulderSubColor1", "shoulder_sub_color_1", ShoulderSubColor1, Codecs.AccentColor);
        owner.Personality = v.BotwPersonality("personality", owner.Personality);
        owner.VoiceType = v.BotwString("voice_type", owner.VoiceType);
    }
}

public sealed class CommonParam : UMiiSection
{
    public Backpack? Backpack { get; set; }
    public Hat? Hat { get; set; }
    public bool? NoHatAlways { get; set; }
    public BodyCorrectType? BodyCorrect { get; set; }
    public bool? IsMidAge { get; set; }
    public float? RotCravicle { get; set; }
    public float? RotArm { get; set; }
    public float? RotLeg { get; set; }
    public float? RotCrotch { get; set; }
    /// <summary>Tears of the Kingdom only.</summary>
    public Vector3? HatTrans { get; set; }
    /// <summary>Tears of the Kingdom only.</summary>
    public Vector3? HatScale { get; set; }
    /// <summary>Tears of the Kingdom only.</summary>
    public float? HatZRotate { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Backpack = v.Enum("Backpack", "backpack", Backpack, Codecs.Backpack);
        Hat = v.Enum("Hat", "hat", Hat, Codecs.Hat);
        NoHatAlways = v.Bool("NoHatAlways", "no_hat_always", NoHatAlways);
        BodyCorrect = v.Enum("BodyCorrect", "body_correct", BodyCorrect, Codecs.BodyCorrect);
        IsMidAge = v.Bool("IsMidAge", "is_mid_age", IsMidAge);
        RotCravicle = v.Float("RotCravicle", "rot_cravicle", RotCravicle);
        RotArm = v.Float("RotArm", "rot_arm", RotArm);
        RotLeg = v.Float("RotLeg", "rot_leg", RotLeg);
        RotCrotch = v.Float("RotCrotch", "rot_crotch", RotCrotch);
        HatTrans = v.Vector3("HatTrans", null, HatTrans);
        HatScale = v.Vector3("HatScale", null, HatScale);
        HatZRotate = v.Float("HatZRotate", null, HatZRotate);
    }
}

/// <summary>Tears of the Kingdom's top level BodyCorrect block (BotW keeps these in Common).</summary>
public sealed class BodyCorrectParam : UMiiSection
{
    public BodyCorrectType? BodyCorrect { get; set; }
    public float? RotArm { get; set; }
    public float? RotCravicle { get; set; }
    public float? RotCrotch { get; set; }
    public float? RotLeg { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        BodyCorrect = v.Enum("BodyCorrect", null, BodyCorrect, Codecs.BodyCorrect);
        RotArm = v.Float("RotArm", null, RotArm);
        RotCravicle = v.Float("RotCravicle", null, RotCravicle);
        RotCrotch = v.Float("RotCrotch", null, RotCrotch);
        RotLeg = v.Float("RotLeg", null, RotLeg);
    }
}

public sealed class ShapeParam : UMiiSection
{
    public int? Jaw { get; set; }
    public int? Wrinkle { get; set; }
    public int? Make { get; set; }
    public float? TransV { get; set; }
    public float? Scale { get; set; }
    public int? SkinColor { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Jaw = v.Int("Jaw", "jaw", Jaw);
        Wrinkle = v.Int("Wrinkle", "wrinkle", Wrinkle);
        Make = v.Int("Make", "make", Make);
        TransV = v.Float("TransV", "trans_v", TransV);
        Scale = v.Float("Scale", "scale", Scale);
        SkinColor = v.Int("SkinColor", "skin_color", SkinColor);
    }
}

public sealed class HairParam : UMiiSection
{
    public int? Type { get; set; }
    public int? Color { get; set; }
    public bool? Flip { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Type = v.Int("Type", "type", Type);
        Color = v.Int("Color", "color", Color);
        Flip = v.Bool("Flip", "flip", Flip);
    }
}

public sealed class EyeParam : UMiiSection
{
    public int? Type { get; set; }
    public int? Color { get; set; }
    public float? TransV { get; set; }
    public float? TransU { get; set; }
    public float? Rotate { get; set; }
    public float? Scale { get; set; }
    public float? Aspect { get; set; }
    public float? EyeballTransU { get; set; }
    public float? EyeballTransV { get; set; }
    public float? EyeballScale { get; set; }
    public int? HighlightBright { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Type = v.Int("Type", "type", Type);
        Color = v.Int("Color", "color", Color);
        TransV = v.Float("TransV", "trans_v", TransV);
        TransU = v.Float("TransU", "trans_u", TransU);
        Rotate = v.Float("Rotate", "rotate", Rotate);
        Scale = v.Float("Scale", "scale", Scale);
        Aspect = v.Float("Aspect", "aspect", Aspect);
        EyeballTransU = v.Float("EyeballTransU", "eyeball_trans_u", EyeballTransU);
        EyeballTransV = v.Float("EyeballTransV", "eyeball_trans_v", EyeballTransV);
        EyeballScale = v.Float("EyeballScale", "eyeball_scale", EyeballScale);
        HighlightBright = v.Int("HighlightBright", "highlight_bright", HighlightBright);
    }
}

public sealed class EyeCtrlParam : UMiiSection
{
    public Vector3? BaseOffset { get; set; }
    public float? TranslimOut { get; set; }
    public float? TranslimIn { get; set; }
    public float? TranslimD { get; set; }
    public float? TranslimU { get; set; }
    public float? NeckOffsetUd { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        BaseOffset = v.Vector3("BaseOffset", "base_offset", BaseOffset);
        TranslimOut = v.Float("TranslimOut", "translim_out", TranslimOut);
        TranslimIn = v.Float("TranslimIn", "translim_in", TranslimIn);
        TranslimD = v.Float("TranslimD", "translim_d", TranslimD);
        TranslimU = v.Float("TranslimU", "translim_u", TranslimU);
        NeckOffsetUd = v.Float("NeckOffsetUd", "neck_offset_ud", NeckOffsetUd);
    }
}

public sealed class EyebrowParam : UMiiSection
{
    public int? Type { get; set; }
    public int? Color { get; set; }
    public float? TransV { get; set; }
    public float? TransU { get; set; }
    public float? Rotate { get; set; }
    public float? Scale { get; set; }
    public float? Aspect { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Type = v.Int("Type", "type", Type);
        Color = v.Int("Color", "color", Color);
        TransV = v.Float("TransV", "trans_v", TransV);
        TransU = v.Float("TransU", "trans_u", TransU);
        Rotate = v.Float("Rotate", "rotate", Rotate);
        Scale = v.Float("Scale", "scale", Scale);
        Aspect = v.Float("Aspect", "aspect", Aspect);
    }
}

public sealed class NoseParam : UMiiSection
{
    public int? Type { get; set; }
    public float? TransV { get; set; }
    public float? Scale { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Type = v.Int("Type", "type", Type);
        TransV = v.Float("TransV", "trans_v", TransV);
        Scale = v.Float("Scale", "scale", Scale);
    }
}

public sealed class MouthParam : UMiiSection
{
    public int? Type { get; set; }
    public int? Color { get; set; }
    public float? TransV { get; set; }
    public float? Scale { get; set; }
    public float? Aspect { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Type = v.Int("Type", "type", Type);
        Color = v.Int("Color", "color", Color);
        TransV = v.Float("TransV", "trans_v", TransV);
        Scale = v.Float("Scale", "scale", Scale);
        Aspect = v.Float("Aspect", "aspect", Aspect);
    }
}

public sealed class BeardParam : UMiiSection
{
    public int? Mustache { get; set; }
    public float? Scale { get; set; }
    public int? Type { get; set; }
    public int? Color { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Mustache = v.Int("Mustache", "mustache", Mustache);
        Scale = v.Float("Scale", "scale", Scale);
        Type = v.Int("Type", "type", Type);
        Color = v.Int("Color", "color", Color);
    }
}

public sealed class GlassParam : UMiiSection
{
    public int? Type { get; set; }
    public int? Color { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Type = v.Int("Type", "type", Type);
        Color = v.Int("Color", "color", Color);
    }
}

public sealed class KorogParam : UMiiSection
{
    public KorogMask? Mask { get; set; }
    public KorogSkinColor? SkinColor { get; set; }
    public KorogPlant? LeftPlant { get; set; }
    public KorogPlant? RightPlant { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Mask = v.Enum("Mask", "mask", Mask, Codecs.KorogMask);
        SkinColor = v.Enum("SkinColor", "skin_color", SkinColor, Codecs.KorogSkinColor);
        LeftPlant = v.Enum("LeftPlant", "left_plant", LeftPlant, Codecs.KorogPlant);
        RightPlant = v.Enum("RightPlant", "right_plant", RightPlant, Codecs.KorogPlant);
    }
}

public sealed class GoronParam : UMiiSection
{
    public int? SkinColor { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        SkinColor = v.Int("SkinColor", "skin_color", SkinColor);
    }
}

public sealed class GerudoParam : UMiiSection
{
    public int? Hair { get; set; }
    public int? HairColor { get; set; }
    public int? Glass { get; set; }
    public int? GlassColor { get; set; }
    public int? SkinColor { get; set; }
    public int? LipColor { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        Hair = v.Int("Hair", "hair", Hair);
        HairColor = v.Int("HairColor", "hair_color", HairColor);
        Glass = v.Int("Glass", "glass", Glass);
        GlassColor = v.Int("GlassColor", "glass_color", GlassColor);
        SkinColor = v.Int("SkinColor", "skin_color", SkinColor);
        LipColor = v.Int("LipColor", "lip_color", LipColor);
    }
}

public sealed class RitoParam : UMiiSection
{
    public int? BodyColor { get; set; }
    /// <summary>-1 means the default.</summary>
    public int? HairColor { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        BodyColor = v.Int("BodyColor", "body_color", BodyColor);
        HairColor = v.Int("HairColor", "hair_color", HairColor);
    }
}

public sealed class ZoraParam : UMiiSection
{
    public int? BodyColor { get; set; }

    internal override void Visit(FieldVisitor v, UMii owner)
    {
        BodyColor = v.Int("BodyColor", "body_color", BodyColor);
    }
}
