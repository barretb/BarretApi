namespace BarretApi.Core.Models;

public static class AvatarStyle
{
    public const string Adventurer = "adventurer";
    public const string AdventurerNeutral = "adventurer-neutral";
    public const string Avataaars = "avataaars";
    public const string AvataaarsNeutral = "avataaars-neutral";
    public const string BigEars = "big-ears";
    public const string BigEarsNeutral = "big-ears-neutral";
    public const string BigSmile = "big-smile";
    public const string Bottts = "bottts";
    public const string BotttsNeutral = "bottts-neutral";
    public const string Croodles = "croodles";
    public const string CroodlesNeutral = "croodles-neutral";
    public const string Dylan = "dylan";
    public const string FunEmoji = "fun-emoji";
    public const string Glass = "glass";
    public const string Icons = "icons";
    public const string Identicon = "identicon";
    public const string Initials = "initials";
    public const string Lorelei = "lorelei";
    public const string LoreleiNeutral = "lorelei-neutral";
    public const string Micah = "micah";
    public const string Miniavs = "miniavs";
    public const string Notionists = "notionists";
    public const string NotionistsNeutral = "notionists-neutral";
    public const string OpenPeeps = "open-peeps";
    public const string Personas = "personas";
    public const string PixelArt = "pixel-art";
    public const string PixelArtNeutral = "pixel-art-neutral";
    public const string Rings = "rings";
    public const string Shapes = "shapes";
    public const string Thumbs = "thumbs";
    public const string ToonHead = "toon-head";

    public static readonly IReadOnlyList<string> All =
    [
        Adventurer, AdventurerNeutral, Avataaars, AvataaarsNeutral,
        BigEars, BigEarsNeutral, BigSmile,
        Bottts, BotttsNeutral,
        Croodles, CroodlesNeutral,
        Dylan, FunEmoji, Glass, Icons, Identicon, Initials,
        Lorelei, LoreleiNeutral,
        Micah, Miniavs,
        Notionists, NotionistsNeutral,
        OpenPeeps, Personas,
        PixelArt, PixelArtNeutral,
        Rings, Shapes, Thumbs, ToonHead
    ];

    public static bool IsValid(string? style)
    {
        return style is not null
            && All.Contains(style, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetRandom()
    {
        return All[Random.Shared.Next(All.Count)];
    }
}
