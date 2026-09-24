# UMiiSharp

A C# reader and writer for **UMii** character files, the parameter sets that describe how NPCs
look in *Breath of the Wild* and *Tears of the Kingdom*:

| Game | File | Format |
| --- | --- | --- |
| BotW | `Actor/UMii/*.bumii` inside `Actor/Pack/*.sbactorpack` | AAMP v2 |
| TotK | `Component/UMiiParam/*.game__component__UMiiParam.bgyml` inside `Pack/Actor/*.pack.zs` | BYML v7 |

Both load into one `UMii` model, and either game's file can be written from it, so a character
can move between games.

```csharp
UMii npc = UMii.FromFile("UMiiVillage003.bumii");          // or FromBinary: the magic decides

npc.Race = Race.Gerudo;
npc.Hair!.Color = 4;
npc.Personal!.FavColor = FavColor.Color9;

File.WriteAllBytes("UMiiVillage003.bumii", npc.ToBumii()); // back to BotW
byte[] totk = npc.ToUMiiParam();                           // or across to TotK (uncompressed)
```

The files come out of packs you open yourself: Yaz0 + SARC for BotW, and zstd + SARC for TotK
(see ZsDicSharp and SarcSharp).

## The model

Every field is nullable, and null means "not in the file". TotK files leave out any value that
matches the game's default, and some BotW files skip parameters too. Nothing gets filled in
behind your back, and absent fields stay absent when you write the file.

Enum member names are the exact strings TotK writes, spelling included (`Race.Shiekah`,
`BodyCorrectType.Non`); the one exception is `BodyHeight.High`, written as `"high"`. The members
come from the game's own enum definitions. BotW stores the same choices as integers, and the
mapping between the two was checked against the 284 characters that appear in both games.

Anything UMiiSharp doesn't model is kept and written back: `ExtraTotkFields` (for example
`BlackboardTableRef`) and `ExtraBotwParameters` / `ExtraBotwObjects`.

## Converting between games

- **TotK to BotW** drops what BotW has nowhere to store: the top-level `BodyCorrect` block, the
  hat transform (`HatTrans`, `HatScale`, `HatZRotate`) and TotK's extra keys. Values BotW can't
  express throw `NotSupportedException`. That covers the teenage ages `SexAge.BT` and `GT`, the
  races after `Race.Other`, and every `Dynamic` value.
- **BotW to TotK** drops `VoiceType`. TotK keeps voices in a separate `VoiceParam` component.

## Fidelity

`UMiiSharp.Verify` runs everything against real romfs dumps:

```bash
dotnet run --project tests/UMiiSharp.Verify -- --botw <botw romfs> --totk <totk romfs>
```

- **BotW:** all 529 `.bumii` files rewrite byte for byte. The writer follows Nintendo's layout,
  including each file's own object and parameter order and value sharing inside larger values.
  Going to TotK and back leaves every value unchanged.
- **TotK:** all 1,864 `UMiiParam` files rewrite byte for byte. Each file's key order is kept,
  because it decides where nested maps land; new files use the case-insensitive order
  Nintendo's files use. 1,837 of them convert to BotW without losing a value. The other 27
  use teenage ages or `Dynamic` values that BotW doesn't have.

## Build

```bash
dotnet build UMiiSharp.sln -c Release
```

The library references AampSharp and BymlSharp from the sibling folders. The verify harness also
references SarcSharp and ZsDicSharp.

## Licence

AGPL-3.0-or-later. See [license.md](license.md).
