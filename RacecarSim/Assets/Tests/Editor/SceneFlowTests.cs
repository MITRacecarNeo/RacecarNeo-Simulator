using NUnit.Framework;
using UnityEngine;

/// <summary>
/// When transitions run (Presentation), and the scene change wipe's geometry: off screen at
/// progress 0 and 2, covering the whole screen at 1.
/// </summary>
public class SceneFlowTests
{
    private const float width = 1920;
    private const float height = 1080;

    [TearDown]
    public void ClearOverride()
    {
        Presentation.Override = null;
    }

    [Test]
    public void ModeText_RaceShowsTheCarCount()
    {
        Assert.AreEqual("Race | 3 cars", SceneFlow.ModeText(LevelManagerMode.Race, 3));
        Assert.AreEqual("Race", SceneFlow.ModeText(LevelManagerMode.Race, 1));
        Assert.AreEqual("Exploration", SceneFlow.ModeText(LevelManagerMode.Exploration, 1));
    }

    [Test]
    public void IsInteractiveRun_OnlyWithoutScriptingOptions()
    {
        Assert.IsTrue(Presentation.IsInteractiveRun(new LaunchOptions.Options(), false), "plain launch");
        Assert.IsFalse(Presentation.IsInteractiveRun(new LaunchOptions.Options(), true), "batch mode");
        Assert.IsFalse(Presentation.IsInteractiveRun(new LaunchOptions.Options { LevelId = "demo-world" }, false), "level requested");
        Assert.IsFalse(Presentation.IsInteractiveRun(new LaunchOptions.Options { AutoStart = true }, false), "autostart");
    }

    [Test]
    public void IsInteractive_OverrideWins()
    {
        Presentation.Override = true;
        Assert.IsTrue(Presentation.IsInteractive);
        Presentation.Override = false;
        Assert.IsFalse(Presentation.IsInteractive);
    }

    [Test]
    public void Cover_OffScreenAtStartAndEnd()
    {
        foreach (Vector2 point in TransitionWipe.Cover(0, SceneFlowTests.width, SceneFlowTests.height))
        {
            Assert.LessOrEqual(point.x, 0, "progress 0 lies left of the screen");
        }
        foreach (Vector2 point in TransitionWipe.Cover(2, SceneFlowTests.width, SceneFlowTests.height))
        {
            Assert.GreaterOrEqual(point.x, SceneFlowTests.width, "progress 2 lies right of the screen");
        }
    }

    [Test]
    public void Cover_FillsScreenAtOne()
    {
        Vector2[] quad = TransitionWipe.Cover(1, SceneFlowTests.width, SceneFlowTests.height);
        Vector2[] corners = { new Vector2(0, 0), new Vector2(SceneFlowTests.width, 0), new Vector2(SceneFlowTests.width, SceneFlowTests.height), new Vector2(0, SceneFlowTests.height) };
        foreach (Vector2 corner in corners)
        {
            Assert.IsTrue(SceneFlowTests.Contains(quad, corner), $"corner {corner} covered");
        }
    }

    [Test]
    public void Edges_MoveLeftToRight()
    {
        float previousRight = float.MinValue;
        for (float p = 0; p <= 1; p += 0.05f)
        {
            float right = TransitionWipe.Edges(p, SceneFlowTests.width, SceneFlowTests.height).Right;
            Assert.GreaterOrEqual(right, previousRight, $"right edge at {p}");
            previousRight = right;
        }
        float previousLeft = float.MinValue;
        for (float p = 1; p <= 2; p += 0.05f)
        {
            float left = TransitionWipe.Edges(p, SceneFlowTests.width, SceneFlowTests.height).Left;
            Assert.GreaterOrEqual(left, previousLeft, $"left edge at {p}");
            previousLeft = left;
        }
    }

    [Test]
    public void Ease_EndsAndMidpoint()
    {
        Assert.AreEqual(0, TransitionWipe.Ease(0), 1e-6);
        Assert.AreEqual(0.5f, TransitionWipe.Ease(0.5f), 1e-6);
        Assert.AreEqual(1, TransitionWipe.Ease(1), 1e-6);
    }

    /// <summary>
    /// True when the point lies inside or on the convex quad (clockwise in screen space).
    /// </summary>
    private static bool Contains(Vector2[] quad, Vector2 point)
    {
        for (int i = 0; i < quad.Length; i++)
        {
            Vector2 a = quad[i];
            Vector2 b = quad[(i + 1) % quad.Length];
            float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
            if (cross < 0)
            {
                return false;
            }
        }
        return true;
    }
}
