namespace SortSharp.SourceGeneration.Templates;

[Flags]
internal enum CallSiteBehaviors : uint
{
    None = 0,
    RepeatCall = 1,
    RepeatArgument0 = 1u << 16,
    RepeatArgument1 = RepeatArgument0 << 1,
    RepeatArgument2 = RepeatArgument0 << 2,
    RepeatArgument3 = RepeatArgument0 << 3,
    RepeatArgument4 = RepeatArgument0 << 4,
    RepeatArgument5 = RepeatArgument0 << 5,
    RepeatArgument6 = RepeatArgument0 << 6,
    RepeatArgument7 = RepeatArgument0 << 7,
    RepeatArgument8 = RepeatArgument0 << 8,
    RepeatArgument9 = RepeatArgument0 << 9,
    RepeatArgument10 = RepeatArgument0 << 10,
    RepeatArgument11 = RepeatArgument0 << 11,
    RepeatArgument12 = RepeatArgument0 << 12,
    RepeatArgument13 = RepeatArgument0 << 13,
    RepeatArgument14 = RepeatArgument0 << 14,
    RepeatArgument15 = RepeatArgument0 << 15,
}
