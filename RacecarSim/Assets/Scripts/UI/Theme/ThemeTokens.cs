// Generated from the RACECAR Neo design tokens. Do not edit by hand.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RACECAR Neo design token values for code that draws UI (gauges, badges, gradients) and for
/// the theme tests. The USS variables in Assets/UI/Theme/Tokens.uss carry the same values.
/// </summary>
public static class ThemeTokens
{
    /// <summary>Factor from design-token px to panel px at the 1920 x 1080 reference.</summary>
    public const float UiScale = 1.5f;

    /// <summary>Lean of the R mark legs and of transition wipes, degrees from vertical.</summary>
    public const float MarkSkewDegrees = 22f;

    /// <summary>Lean of parallelogram badges such as the lap timer, degrees.</summary>
    public const float BadgeSkewDegrees = 15f;

    public static readonly Color32 CrimsonDeep = new Color32(0x85, 0x00, 0x28, 0xFF);
    public static readonly Color32 Crimson = new Color32(0xA0, 0x19, 0x36, 0xFF);
    public static readonly Color32 Red = new Color32(0xBC, 0x33, 0x45, 0xFF);
    public static readonly Color32 Coral = new Color32(0xDF, 0x7A, 0x46, 0xFF);
    public static readonly Color32 Orange = new Color32(0xF4, 0x9A, 0x4B, 0xFF);
    public static readonly Color32 Gold = new Color32(0xFF, 0xB8, 0x4A, 0xFF);
    public static readonly Color32 Ink = new Color32(0x23, 0x1F, 0x20, 0xFF);
    public static readonly Color32 Charcoal = new Color32(0x2F, 0x2D, 0x38, 0xFF);
    public static readonly Color32 Slate = new Color32(0x42, 0x3F, 0x52, 0xFF);
    public static readonly Color32 Steel = new Color32(0x50, 0x51, 0x66, 0xFF);
    public static readonly Color32 Mist = new Color32(0xE3, 0xE3, 0xEB, 0xFF);
    public static readonly Color32 Paper = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    public static readonly Color32 HudBlue = new Color32(0x33, 0x55, 0xAA, 0xFF);
    public static readonly Color32 LedRed = new Color32(0xF6, 0x3C, 0x47, 0xFF);
    public static readonly Color32 Success = new Color32(0x19, 0x68, 0x6C, 0xFF);

    /// <summary>Token path to color, for every color the UI may use.</summary>
    public static readonly IReadOnlyDictionary<string, Color32> Colors = new Dictionary<string, Color32>
    {
        { "brand.crimson-deep", ThemeTokens.CrimsonDeep },
        { "brand.crimson", ThemeTokens.Crimson },
        { "brand.red", ThemeTokens.Red },
        { "brand.coral", ThemeTokens.Coral },
        { "brand.orange", ThemeTokens.Orange },
        { "brand.gold", ThemeTokens.Gold },
        { "neutral.ink", ThemeTokens.Ink },
        { "neutral.charcoal", ThemeTokens.Charcoal },
        { "neutral.slate", ThemeTokens.Slate },
        { "neutral.steel", ThemeTokens.Steel },
        { "neutral.mist", ThemeTokens.Mist },
        { "neutral.paper", ThemeTokens.Paper },
        { "hud.blue", ThemeTokens.HudBlue },
        { "hud.led-red", ThemeTokens.LedRed },
        { "illustration.day.grass-dark", ThemeTokens.Success },
    };

    /// <summary>USS variable name (without --rcn-) to token path, for the palette variables.</summary>
    public static readonly IReadOnlyDictionary<string, string> UssNames = new Dictionary<string, string>
    {
        { "crimson-deep", "brand.crimson-deep" },
        { "crimson", "brand.crimson" },
        { "red", "brand.red" },
        { "coral", "brand.coral" },
        { "orange", "brand.orange" },
        { "gold", "brand.gold" },
        { "ink", "neutral.ink" },
        { "charcoal", "neutral.charcoal" },
        { "slate", "neutral.slate" },
        { "steel", "neutral.steel" },
        { "mist", "neutral.mist" },
        { "paper", "neutral.paper" },
        { "hud-blue", "hud.blue" },
        { "led-red", "hud.led-red" },
        { "success", "illustration.day.grass-dark" },
    };

    /// <summary>Dark semantic role (USS name without --rcn-) to token path or literal hex.</summary>
    public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>
    {
        { "bg", "neutral.charcoal" },
        { "surface", "neutral.slate" },
        { "text", "neutral.paper" },
        { "text-muted", "neutral.mist" },
        { "accent", "brand.orange" },
        { "on-accent", "neutral.ink" },
        { "accent-hover", "brand.gold" },
        { "link", "brand.orange" },
        { "highlight", "brand.gold" },
        { "on-highlight", "neutral.ink" },
        { "border", "neutral.steel" },
        { "focus", "brand.gold" },
        { "code-bg", "#232228" },
        { "code-text", "neutral.mist" },
        { "overlay", "neutral.charcoal" },
    };

    /// <summary>Text and background pairs approved for UI text, with the minimum WCAG ratio.</summary>
    public static readonly (string Foreground, string Background, float Minimum)[] ApprovedPairs =
    {
        ("neutral.ink", "neutral.paper", 4.5f),
        ("neutral.ink", "neutral.mist", 4.5f),
        ("neutral.steel", "neutral.paper", 4.5f),
        ("brand.crimson", "neutral.paper", 4.5f),
        ("brand.crimson", "neutral.mist", 4.5f),
        ("neutral.paper", "brand.crimson", 4.5f),
        ("neutral.paper", "brand.crimson-deep", 4.5f),
        ("neutral.paper", "brand.red", 4.5f),
        ("neutral.ink", "brand.orange", 4.5f),
        ("neutral.ink", "brand.coral", 4.5f),
        ("neutral.ink", "brand.gold", 4.5f),
        ("neutral.paper", "neutral.charcoal", 4.5f),
        ("neutral.mist", "neutral.charcoal", 4.5f),
        ("brand.orange", "neutral.charcoal", 4.5f),
        ("brand.gold", "neutral.charcoal", 4.5f),
        ("brand.coral", "neutral.charcoal", 4.5f),
        ("neutral.paper", "neutral.slate", 4.5f),
        ("illustration.day.grass-dark", "neutral.paper", 4.5f),
    };

    /// <summary>Pairs that must never carry text.</summary>
    public static readonly (string Foreground, string Background)[] ForbiddenPairs =
    {
        ("neutral.paper", "brand.orange"),
        ("neutral.paper", "brand.gold"),
        ("neutral.paper", "brand.coral"),
        ("brand.crimson", "neutral.charcoal"),
        ("brand.crimson-deep", "neutral.charcoal"),
    };

    /// <summary>gradient.mark stops, left to right: crimson to 22 percent, then to orange.</summary>
    public static readonly (float Position, Color32 Color)[] MarkGradient =
    {
        (0f, ThemeTokens.Crimson),
        (0.22f, ThemeTokens.Crimson),
        (1f, ThemeTokens.Orange),
    };

    /// <summary>Scenery palette variants (illustration.day, .sunset, .autumn), token path to color.</summary>
    public static readonly IReadOnlyDictionary<string, Color32> Scenery = new Dictionary<string, Color32>
    {
        { "illustration.day.sky", new Color32(0x84, 0xDD, 0xFF, 0xFF) },
        { "illustration.day.cloud", new Color32(0xA2, 0xF2, 0xF3, 0xFF) },
        { "illustration.day.sun", new Color32(0xDC, 0xFF, 0xE3, 0xFF) },
        { "illustration.day.mountain-light", new Color32(0x41, 0xB8, 0xEC, 0xFF) },
        { "illustration.day.mountain-mid", new Color32(0x31, 0x86, 0xDC, 0xFF) },
        { "illustration.day.mountain-dark", new Color32(0x33, 0x55, 0xA9, 0xFF) },
        { "illustration.day.grass-light", new Color32(0x4F, 0xC8, 0x5B, 0xFF) },
        { "illustration.day.grass-mid", new Color32(0x22, 0x97, 0x69, 0xFF) },
        { "illustration.day.grass-dark", new Color32(0x19, 0x68, 0x6C, 0xFF) },
        { "illustration.day.road", new Color32(0x42, 0x3F, 0x52, 0xFF) },
        { "illustration.day.road-shadow", new Color32(0x34, 0x32, 0x3F, 0xFF) },
        { "illustration.day.road-line", new Color32(0x56, 0x5A, 0x71, 0xFF) },
        { "illustration.sunset.sky", new Color32(0xFF, 0xAF, 0x4B, 0xFF) },
        { "illustration.sunset.cloud", new Color32(0xFF, 0xDF, 0x57, 0xFF) },
        { "illustration.sunset.sun", new Color32(0xFF, 0xFA, 0xBB, 0xFF) },
        { "illustration.sunset.mountain-light", new Color32(0xFF, 0x76, 0x48, 0xFF) },
        { "illustration.sunset.mountain-mid", new Color32(0xD6, 0x61, 0x0F, 0xFF) },
        { "illustration.sunset.ridge", new Color32(0x94, 0x3D, 0x45, 0xFF) },
        { "illustration.sunset.ridge-dark", new Color32(0x6F, 0x2E, 0x34, 0xFF) },
        { "illustration.sunset.shadow", new Color32(0x25, 0x0F, 0x11, 0xFF) },
        { "illustration.autumn.sky", new Color32(0x82, 0xC0, 0xE9, 0xFF) },
        { "illustration.autumn.water", new Color32(0xBC, 0xE4, 0xF7, 0xFF) },
        { "illustration.autumn.rock", new Color32(0x96, 0x90, 0x87, 0xFF) },
        { "illustration.autumn.rock-shadow", new Color32(0x89, 0x82, 0x7F, 0xFF) },
        { "illustration.autumn.ground", new Color32(0x6F, 0x55, 0x47, 0xFF) },
        { "illustration.autumn.ground-shadow", new Color32(0x65, 0x4C, 0x41, 0xFF) },
        { "illustration.autumn.sand", new Color32(0xF0, 0xB9, 0x62, 0xFF) },
        { "illustration.autumn.leaf-yellow", new Color32(0xFF, 0xC7, 0x50, 0xFF) },
        { "illustration.autumn.leaf-orange", new Color32(0xEB, 0x7B, 0x3F, 0xFF) },
        { "illustration.autumn.leaf-red", new Color32(0xD2, 0x62, 0x3E, 0xFF) },
        { "illustration.autumn.trunk", new Color32(0xC1, 0x93, 0x6D, 0xFF) },
    };
}
