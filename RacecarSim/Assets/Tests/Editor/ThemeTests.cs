using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// UI theme rules from the RACECAR Neo design standards: text only on approved color pairs, no
/// color literals outside Tokens.uss, blue only on the lap timer, and license files beside every
/// shipped font.
/// </summary>
public class ThemeTests
{
    private static readonly string uiFolder = Path.Combine(Application.dataPath, "UI");
    private static readonly string tokensUss = Path.Combine(ThemeTests.uiFolder, "Theme", "Tokens.uss");
    private static readonly string fontsFolder = Path.Combine(Application.dataPath, "Fonts");

    // Pairs used outside the approved list, each still held to 4.5:1: the dark code block colors.
    private static readonly (string Foreground, string Background)[] motifPairs =
    {
        ("neutral.mist", "#232228"),
    };

    // Display-size text held to the 3:1 large-text minimum instead of 4.5:1: the HUD failure
    // card's title and icon in LED red.
    private static readonly (string Foreground, string Background, string Selector)[] largeTextPairs =
    {
        ("hud.led-red", "neutral.charcoal", ".hud-result--failed .hud-result-title"),
        ("hud.led-red", "neutral.charcoal", ".hud-result--failed .hud-result-icon"),
    };

    private static readonly Regex variablePattern = new Regex(@"^var\(--rcn-([\w-]+)\)$");
    private static readonly Regex literalPattern = new Regex(@"#[0-9a-fA-F]{3,8}\b|rgba?\(");
    private const string transparent = "rgba(0, 0, 0, 0)";

    /// <summary>
    /// Literals allowed outside the palette: the shell color slider thumbs show the full channel
    /// colors they set on the car.
    /// </summary>
    private static readonly Dictionary<string, string> channelColors = new Dictionary<string, string>
    {
        { ".mn-slider--r .unity-base-slider__dragger", "rgb(255, 0, 0)" },
        { ".mn-slider--g .unity-base-slider__dragger", "rgb(0, 255, 0)" },
        { ".mn-slider--b .unity-base-slider__dragger", "rgb(0, 0, 255)" },
    };

    [Test]
    public void ApprovedPairs_MeetTheirMinimum()
    {
        foreach ((string fg, string bg, float minimum) in ThemeTokens.ApprovedPairs)
        {
            Assert.GreaterOrEqual(ThemeContrast.Ratio(ThemeTokens.Colors[fg], ThemeTokens.Colors[bg]), minimum, $"{fg} on {bg}");
        }
    }

    [Test]
    public void ForbiddenPairs_FallBelowBodyText()
    {
        foreach ((string fg, string bg) in ThemeTokens.ForbiddenPairs)
        {
            Assert.Less(ThemeContrast.Ratio(ThemeTokens.Colors[fg], ThemeTokens.Colors[bg]), 4.5f, $"{fg} on {bg}");
        }
    }

    [Test]
    public void TokensUss_MatchesThemeTokens()
    {
        Dictionary<string, string> variables = ThemeTests.Declarations(File.ReadAllText(ThemeTests.tokensUss)).SelectMany(rule => rule.Properties)
            .Where(p => p.Key.StartsWith("--rcn-")).ToDictionary(p => p.Key.Substring("--rcn-".Length), p => p.Value);
        foreach (KeyValuePair<string, string> name in ThemeTokens.UssNames)
        {
            Assert.AreEqual(ThemeTests.Hex(ThemeTokens.Colors[name.Value]), variables[name.Key].ToUpperInvariant(), name.Key);
        }
        foreach (KeyValuePair<string, string> role in ThemeTokens.Roles.Where(r => r.Key != "overlay"))
        {
            Assert.AreEqual(ThemeTests.Hex(ThemeTests.Resolve(role.Value)), variables[role.Key].ToUpperInvariant(), role.Key);
        }
    }

    [Test]
    public void StyleSheets_SetTextOnlyOnApprovedPairs()
    {
        HashSet<(string, string)> allowed = new HashSet<(string, string)>(ThemeTokens.ApprovedPairs.Select(p => (p.Foreground, p.Background)).Concat(ThemeTests.motifPairs));
        List<string> problems = new List<string>();
        foreach ((string file, string selector, Dictionary<string, string> properties) in ThemeTests.StyleRules())
        {
            if (!properties.TryGetValue("color", out string colorValue))
            {
                continue;
            }

            string fg = ThemeTests.TokenOf(colorValue);
            string bgValue = properties.TryGetValue("background-color", out string background) && background != ThemeTests.transparent ? background
                : properties.TryGetValue("--fill", out string fill) ? fill
                : "var(--rcn-bg)";
            string bg = ThemeTests.TokenOf(bgValue);
            if (fg == null || bg == null)
            {
                problems.Add($"{file} {selector}: color not from a token ({colorValue} on {bgValue})");
                continue;
            }

            float ratio = ThemeContrast.Ratio(ThemeTests.Resolve(fg), ThemeTests.Resolve(bg));
            bool isLargeText = ThemeTests.largeTextPairs.Contains((fg, bg, selector));
            if (isLargeText ? ratio < 3 : !allowed.Contains((fg, bg)) || ratio < 4.5f)
            {
                problems.Add($"{file} {selector}: {fg} on {bg} ({ratio:F2}:1)");
            }
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void StyleSheets_UseOnlyTokenColors()
    {
        List<string> problems = new List<string>();
        foreach ((string file, string selector, Dictionary<string, string> properties) in ThemeTests.StyleRules())
        {
            foreach (KeyValuePair<string, string> property in properties)
            {
                bool isChannel = ThemeTests.channelColors.TryGetValue(selector, out string channel) && property.Value == channel;
                if (property.Value != ThemeTests.transparent && !isChannel && ThemeTests.literalPattern.IsMatch(property.Value))
                {
                    problems.Add($"{file} {selector} {property.Key}: {property.Value}");
                }
            }
        }
        foreach (string uxml in Directory.GetFiles(Application.dataPath, "*.uxml", SearchOption.AllDirectories))
        {
            foreach (Match style in Regex.Matches(File.ReadAllText(uxml), "style=\"([^\"]*)\""))
            {
                if (ThemeTests.literalPattern.IsMatch(style.Groups[1].Value))
                {
                    problems.Add($"{ThemeTests.Relative(uxml)}: inline color {style.Groups[1].Value}");
                }
            }
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void HudBlue_OnlyOnTheTimer()
    {
        List<string> problems = ThemeTests.StyleRules()
            .Where(rule => rule.Properties.Values.Any(v => v.Contains("--rcn-hud-blue")) && !rule.Selector.Contains("timer"))
            .Select(rule => $"{rule.File} {rule.Selector}").ToList();
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void Fonts_ShipWithLicense()
    {
        foreach (string font in Directory.GetFiles(ThemeTests.fontsFolder, "*.ttf", SearchOption.AllDirectories)
            .Where(f => Path.GetFullPath(Path.GetDirectoryName(f)) != Path.GetFullPath(ThemeTests.fontsFolder)))
        {
            Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(font), "OFL.txt")), $"no OFL.txt beside {ThemeTests.Relative(font)}");
        }
    }

    /// <summary>
    /// Token path (or literal hex for roles defined by a literal) for a var(--rcn-*) value.
    /// </summary>
    private static string TokenOf(string value)
    {
        Match match = ThemeTests.variablePattern.Match(value.Trim());
        if (!match.Success)
        {
            return null;
        }
        string name = match.Groups[1].Value;
        return ThemeTokens.UssNames.TryGetValue(name, out string path) ? path
            : ThemeTokens.Roles.TryGetValue(name, out string role) ? role
            : null;
    }

    private static Color32 Resolve(string tokenOrHex)
    {
        if (ThemeTokens.Colors.TryGetValue(tokenOrHex, out Color32 color))
        {
            return color;
        }
        Assert.IsTrue(ColorUtility.TryParseHtmlString(tokenOrHex, out Color parsed), $"unknown color {tokenOrHex}");
        return parsed;
    }

    private static string Hex(Color32 color)
    {
        return $"#{color.r:X2}{color.g:X2}{color.b:X2}";
    }

    private static string Relative(string path)
    {
        return Path.GetRelativePath(Path.GetDirectoryName(Application.dataPath), path).Replace('\\', '/');
    }

    private static IEnumerable<(string File, string Selector, Dictionary<string, string> Properties)> StyleRules()
    {
        foreach (string uss in Directory.GetFiles(ThemeTests.uiFolder, "*.uss", SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(uss) == Path.GetFullPath(ThemeTests.tokensUss))
            {
                continue;
            }
            foreach ((string selector, Dictionary<string, string> properties) in ThemeTests.Declarations(File.ReadAllText(uss)))
            {
                yield return (ThemeTests.Relative(uss), selector, properties);
            }
        }
    }

    private static IEnumerable<(string Selector, Dictionary<string, string> Properties)> Declarations(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
        foreach (Match rule in Regex.Matches(text, @"([^{}]+)\{([^{}]*)\}"))
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            foreach (Match property in Regex.Matches(rule.Groups[2].Value, @"([\w-]+)\s*:\s*([^;]+);"))
            {
                properties[property.Groups[1].Value] = property.Groups[2].Value.Trim();
            }
            yield return (rule.Groups[1].Value.Trim(), properties);
        }
    }
}
