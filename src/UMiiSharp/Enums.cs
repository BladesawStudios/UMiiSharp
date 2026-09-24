namespace UMiiSharp;

// Member names are the exact strings Tears of the Kingdom writes to UMiiParam, spelling included
// ("Shiekah", "Non", "high"). Breath of the Wild stores the same choices as integers; the
// conversions live in BotwCodes.

public enum Race
{
    Hylia,
    Korog,
    Goron,
    /// <summary>Sheikah. The game spells it this way.</summary>
    Shiekah,
    Gerudo,
    Zora,
    Rito,
    /// <summary>Non-UMii characters: Breath of the Wild uses it for the Great Fairies and Malanya.</summary>
    Other,
    AncientGerudo,
    AncientHylia,
    ZonauGolem,
    Zonau,
    Assassin,
}

public enum SexAge
{
    /// <summary>Boy.</summary>
    B,
    /// <summary>Teenage boy (Tears of the Kingdom only).</summary>
    BT,
    /// <summary>Man.</summary>
    M,
    /// <summary>Old man.</summary>
    X,
    /// <summary>Girl.</summary>
    G,
    /// <summary>Teenage girl (Tears of the Kingdom only).</summary>
    GT,
    /// <summary>Woman.</summary>
    W,
    /// <summary>Old woman.</summary>
    Y,
}

public enum Personality
{
    BoyNaughty,
    BoyDocile,
    GirlNaughty,
    GirlDocile,
    ManNormal,
    ManActive,
    ManDeflated,
    WomanNormal,
    WomanActive,
    WomanDeflated,
    OldmanNormal,
    OldmanActive,
    OldmanDeflated,
    OldwomanNormal,
    OldwomanActive,
    OldwomanDeflated,
}

/// <summary>
/// The whole enum, per TotK 1.2.1's PropAccessor for game::umii::Ffsd::Type (the name table at
/// 0x7104351690 holds only "Mii" and "Random"). BotW's integer uses the same numbering.
/// </summary>
public enum FfsdType
{
    Mii,
    Random,
}

public enum BodyType
{
    C,
    N,
    T,
    S,
    SK,
    U,
    H,
    SB,
    CV,
    SV,
    O,
    SS,
    A,
    TE,
    None,
}

public enum BodyNumber
{
    Number0,
    Number1,
    Number2,
    Number3,
    Number4,
    Number5,
    Number6,
    Number7,
    Number8,
    Number9,
    Number10,
    Number11,
    Number20,
    Number300,
    None,
}

public enum BodyWeight
{
    Thin,
    Standard,
    Thick,
}

public enum BodyHeight
{
    Standard,
    /// <summary>Written as lowercase "high" by the game.</summary>
    High,
}

public enum BodyCorrectType
{
    Non,
    UMii,
    Original,
}

public enum Backpack
{
    TypeNone,
    Type0,
    Type1,
    Type2,
    Type3,
    Type4,
}

/// <summary>Hat models. There is no Type15.</summary>
public enum Hat
{
    None,
    Type0,
    Type1,
    Type2,
    Type3,
    Type4,
    Type5,
    Type6,
    Type7,
    Type8,
    Type9,
    Type10,
    Type11,
    Type12,
    Type13,
    Type14,
    Type16,
    Type17,
    Type18,
    Type19,
    Type200,
}

/// <summary>The main clothing colour.</summary>
public enum FavColor
{
    Color0,
    Color1,
    Color2,
    Color3,
    Color4,
    Color5,
    Color6,
    Color7,
    Color8,
    Color9,
    Color10,
    Color11,
    Color12,
    Color13,
    Color14,
    None,
}

/// <summary>
/// The head, shoulder and shoulder-sub colours. The game's three enums share this shape;
/// head colours stop at Color11, only the shoulder colour takes Dynamic, and the
/// SubColor12-14 names are the game's own.
/// </summary>
public enum AccentColor
{
    ColorNone,
    Color0,
    Color1,
    Color2,
    Color3,
    Color4,
    Color5,
    Color6,
    Color7,
    Color8,
    Color9,
    Color10,
    Color11,
    SubColor12,
    SubColor13,
    SubColor14,
    Dynamic,
}

public enum SubColor
{
    SubColorNone,
    SubColor00,
    SubColor01,
    SubColor02,
    SubColor03,
    SubColor04,
    SubColor05,
    SubColor06,
    SubColor07,
    SubColor08,
    SubColor09,
    SubColor10,
    SubColor11,
    SubColor12,
    SubColor13,
    SubColor14,
}

public enum KorogMask
{
    Mask00,
    Mask01,
    Mask02,
    Mask03,
    Mask04,
    Mask05,
    Mask06,
    Mask07,
    Mask08,
    Random,
    Dynamic,
}

public enum KorogPlant
{
    None,
    Plant00,
    Plant01,
    Random,
    Dynamic,
}

public enum KorogSkinColor
{
    Color00,
    Color01,
    Color02,
    Color03,
    Color04,
    Random,
    Dynamic,
}
