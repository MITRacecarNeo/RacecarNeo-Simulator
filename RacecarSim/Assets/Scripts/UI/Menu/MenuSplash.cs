using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The splash sequence on the main menu's first appearance in a run (interactive runs only): on a
/// charcoal cover the gradient bar draws in, the logo slides in from the left and settles, then
/// the cover wipes away to Home while the turntable car spins in. Any key,
/// mouse button, or controller button skips to the wipe.
/// </summary>
public static class MenuSplash
{
    /// <summary>
    /// Seconds for the gradient bar to draw in.
    /// </summary>
    public const float BarSeconds = 0.5f;

    /// <summary>
    /// Seconds for the logo to slide in and settle.
    /// </summary>
    public const float LogoSeconds = 0.8f;

    /// <summary>
    /// Seconds the settled logo holds before the wipe.
    /// </summary>
    public const float HoldSeconds = 1.1f;

    /// <summary>
    /// Seconds for the cover to wipe away.
    /// </summary>
    public const float RevealSeconds = 0.6f;

    /// <summary>
    /// Distance the logo slides in from, in panel pixels.
    /// </summary>
    private const float slideDistance = 160;

    /// <summary>
    /// True once the splash played (or was skipped) in this run; settable for tests.
    /// </summary>
    public static bool HasPlayed;

    /// <summary>
    /// True when the splash should play on this menu load.
    /// </summary>
    public static bool ShouldPlay => Presentation.IsInteractive && !MenuSplash.HasPlayed;

    /// <summary>
    /// Plays the sequence on the splash element (MainMenu.uxml "splash") and hides it at the end.
    /// </summary>
    public static IEnumerator Play(VisualElement splash, MenuShowcase showcase)
    {
        MenuSplash.HasPlayed = true;
        TransitionWipe cover = splash.Q<TransitionWipe>("splash-cover");
        VisualElement logo = splash.Q("splash-logo");
        GradientBar bar = splash.Q<GradientBar>("splash-bar");

        splash.style.display = DisplayStyle.Flex;
        cover.Progress = 1;
        bar.Fill = 0;
        logo.style.opacity = 0;

        bool skipped = false;
        float t = 0;
        float total = MenuSplash.BarSeconds + MenuSplash.LogoSeconds + MenuSplash.HoldSeconds;
        while (t < total && !skipped)
        {
            bar.Fill = TransitionWipe.Ease(t / MenuSplash.BarSeconds);

            float logoT = Mathf.Clamp01((t - MenuSplash.BarSeconds * 0.4f) / MenuSplash.LogoSeconds);
            float eased = 1 - Mathf.Pow(1 - logoT, 3);
            logo.style.opacity = eased;
            logo.style.translate = new Translate(-MenuSplash.slideDistance * (1 - eased), 0);

            yield return null;
            t += Time.unscaledDeltaTime;
            skipped = MenuSplash.SkipPressed();
        }

        bar.Fill = 1;
        logo.style.opacity = 1;
        logo.style.translate = new Translate(0, 0);

        showcase.SpinIn(MenuSplash.RevealSeconds * 2);
        for (float r = 0; r < MenuSplash.RevealSeconds; r += Mathf.Min(Time.unscaledDeltaTime, 1 / 30f))
        {
            float fade = 1 - Mathf.Clamp01(r / (MenuSplash.RevealSeconds * 0.4f));
            logo.style.opacity = fade;
            bar.style.opacity = fade;
            cover.Progress = 1 + r / MenuSplash.RevealSeconds;
            yield return null;
        }
        splash.style.display = DisplayStyle.None;
    }

    private static bool SkipPressed()
    {
        return Input.anyKeyDown
            || Controller.WasPressed(Controller.Button.A)
            || Controller.WasPressed(Controller.Button.B)
            || Controller.WasPressed(Controller.Button.START);
    }
}
