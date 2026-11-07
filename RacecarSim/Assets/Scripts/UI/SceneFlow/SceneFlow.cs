using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// How a scene change looks.
/// </summary>
public enum SceneTransition
{
    /// <summary>
    /// The cover slides in before the load, for loads started from a menu.
    /// </summary>
    Wipe,

    /// <summary>
    /// The cover appears at once and the load starts in the same frame, for loads started inside
    /// a level so no frame runs between the request and the load.
    /// </summary>
    Cut,
}

/// <summary>
/// Every scene change goes through here. Loads stay synchronous, as before; in interactive runs a
/// persistent overlay covers the screen for the change and slides away in the new scene. A load
/// of the ReloadBuffer scene keeps the cover up until the buffer loads the level again.
/// Non-interactive runs (Presentation.IsInteractive false) load directly with no overlay.
/// </summary>
public class SceneFlow : MonoBehaviour
{
    #region Constants
    /// <summary>
    /// Seconds for the cover to slide in (Wipe).
    /// </summary>
    public const float CoverSeconds = 0.35f;

    /// <summary>
    /// Seconds the loading card shows on the cover before a Wipe load starts, so its fade-in
    /// finishes before the synchronous load holds the frame.
    /// </summary>
    public const float CardFadeSeconds = 0.4f;

    /// <summary>
    /// Shortest time the loading card stays on screen, load time included.
    /// </summary>
    public const float CardMinimumSeconds = 2.0f;

    /// <summary>
    /// Seconds for the cover to slide out in the new scene.
    /// </summary>
    public const float RevealSeconds = 0.4f;

    /// <summary>
    /// Frames the new scene runs under the cover before the reveal starts.
    /// </summary>
    private const int settleFrames = 2;

    /// <summary>
    /// Longest frame step the animation takes, so a slow load frame does not skip the slide.
    /// </summary>
    private const float maxStep = 1 / 30f;

    private const string prefabResource = "SceneFlowOverlay";
    #endregion

    #region Set in Unity Editor
    /// <summary>
    /// The overlay document (SceneFlowOverlay.uxml on the overlay panel settings).
    /// </summary>
    [SerializeField]
    private UIDocument document;
    #endregion

    private enum State
    {
        Idle,
        Covering,
        Loading,
        Settling,
        Held,
        Revealing,
    }

    private static SceneFlow instance;

    private State state;
    private VisualElement root;
    private TransitionWipe wipe;
    private VisualElement card;
    private VisualElement cardThumbnail;
    private Label cardCollection;
    private Label cardTitle;
    private Label cardMode;

    #region Public Interface
    /// <summary>
    /// True from a load request until the cover has slid away in the new scene.
    /// </summary>
    public static bool IsTransitioning => SceneFlow.instance != null && SceneFlow.instance.state != State.Idle;

    /// <summary>
    /// Loads the level in LevelManager.LevelInfo at the given build index from a menu: the cover
    /// slides in and shows the level's loading card first.
    /// </summary>
    public static void LoadLevel(int buildIndex)
    {
        _ = SceneFlow.Begin(() => SceneManager.LoadScene(buildIndex, LoadSceneMode.Single), SceneTransition.Wipe, LevelManager.LevelInfo, false, null, 0);
    }

    /// <summary>
    /// Loads a scene by build index. beforeLoad runs right before the load, after the cover is up.
    /// holdSeconds keeps the current frame on screen before the cover starts (interactive runs
    /// only); other requests are ignored meanwhile. Returns false when the request was ignored
    /// because another change is covering or loading.
    /// </summary>
    public static bool Load(int buildIndex, SceneTransition transition, Action beforeLoad = null, float holdSeconds = 0)
    {
        return SceneFlow.Begin(() => SceneManager.LoadScene(buildIndex, LoadSceneMode.Single), transition, null, buildIndex == ReloadBuffer.BuildIndex, beforeLoad, holdSeconds);
    }

    /// <summary>
    /// Loads a scene by name; see Load(int, SceneTransition, Action, float).
    /// </summary>
    public static bool Load(string sceneName, SceneTransition transition, Action beforeLoad = null, float holdSeconds = 0)
    {
        return SceneFlow.Begin(() => SceneManager.LoadScene(sceneName, LoadSceneMode.Single), transition, null, false, beforeLoad, holdSeconds);
    }
    #endregion

    private void Awake()
    {
        this.root = this.document.rootVisualElement.Q("scene-flow");
        this.wipe = this.root.Q<TransitionWipe>("wipe");
        this.card = this.root.Q("card");
        this.cardThumbnail = this.root.Q("card-thumbnail");
        this.cardCollection = this.root.Q<Label>("card-collection");
        this.cardTitle = this.root.Q<Label>("card-title");
        this.cardMode = this.root.Q<Label>("card-mode");
        this.SetVisible(false);
    }

    private static bool Begin(Action load, SceneTransition transition, LevelInfo cardLevel, bool holdCover, Action beforeLoad, float holdSeconds)
    {
        if (beforeLoad != null)
        {
            Action loadOnly = load;
            load = () =>
            {
                beforeLoad();
                loadOnly();
            };
        }

        if (!Presentation.IsInteractive)
        {
            load();
            return true;
        }

        SceneFlow flow = SceneFlow.Instance();
        if (flow == null)
        {
            load();
            return true;
        }
        // A request once the new scene is in (Esc right after a level appears) cuts the reveal short
        if (flow.state == State.Covering || flow.state == State.Loading)
        {
            Debug.LogWarning("[SceneFlow]: a scene change is already in progress; request ignored.");
            return false;
        }
        flow.StopAllCoroutines();
        flow.StartCoroutine(flow.Run(load, transition, cardLevel, holdCover, holdSeconds));
        return true;
    }

    private static SceneFlow Instance()
    {
        if (SceneFlow.instance == null)
        {
            SceneFlow prefab = Resources.Load<SceneFlow>(SceneFlow.prefabResource);
            if (prefab == null)
            {
                Debug.LogError($"[SceneFlow]: no {SceneFlow.prefabResource} prefab in Resources; loading without transitions.");
                return null;
            }
            SceneFlow.instance = Instantiate(prefab);
            SceneFlow.instance.name = SceneFlow.prefabResource;
            DontDestroyOnLoad(SceneFlow.instance.gameObject);
        }
        return SceneFlow.instance;
    }

    private IEnumerator Run(Action load, SceneTransition transition, LevelInfo cardLevel, bool holdCover, float holdSeconds)
    {
        bool alreadyCovered = this.state == State.Settling || this.state == State.Held || this.state == State.Revealing;
        if (!alreadyCovered)
        {
            this.SetMode(LevelManager.LevelManagerMode);
        }
        if (holdSeconds > 0)
        {
            this.state = State.Covering;
            yield return SceneFlow.WaitUnscaled(holdSeconds);
        }
        this.state = State.Covering;
        this.SetVisible(true);

        if (transition == SceneTransition.Wipe && !alreadyCovered)
        {
            yield return this.Animate(0, 1, SceneFlow.CoverSeconds);
        }
        this.wipe.Progress = 1;

        float cardShownAt = -1;
        if (cardLevel != null && transition == SceneTransition.Wipe)
        {
            this.ShowCard(cardLevel);
            cardShownAt = Time.realtimeSinceStartup;
            yield return SceneFlow.WaitUnscaled(SceneFlow.CardFadeSeconds);
        }

        this.state = State.Loading;
        bool loaded = false;
        void OnLoaded(Scene scene, LoadSceneMode mode) => loaded = true;
        SceneManager.sceneLoaded += OnLoaded;
        try
        {
            load();
            while (!loaded)
            {
                yield return null;
            }
        }
        finally
        {
            SceneManager.sceneLoaded -= OnLoaded;
        }
        this.state = State.Settling;
        for (int i = 0; i < SceneFlow.settleFrames; i++)
        {
            yield return null;
        }

        if (holdCover)
        {
            this.state = State.Held;
            yield break;
        }

        if (cardShownAt >= 0)
        {
            float remaining = SceneFlow.CardMinimumSeconds - (Time.realtimeSinceStartup - cardShownAt);
            yield return SceneFlow.WaitUnscaled(remaining);
        }

        this.state = State.Revealing;
        this.card.RemoveFromClassList("sf-card--visible");
        yield return this.Animate(1, 2, SceneFlow.RevealSeconds);
        this.SetVisible(false);
        this.state = State.Idle;
    }

    private IEnumerator Animate(float from, float to, float seconds)
    {
        float t = 0;
        while (t < seconds)
        {
            this.wipe.Progress = Mathf.Lerp(from, to, t / seconds);
            yield return null;
            t += Mathf.Min(Time.unscaledDeltaTime, SceneFlow.maxStep);
        }
        this.wipe.Progress = to;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0;
        while (t < seconds)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
        }
    }

    private void ShowCard(LevelInfo level)
    {
        Texture2D thumbnail = LevelPresentationCatalog.Get(level).Thumbnail;
        this.cardThumbnail.style.backgroundImage = thumbnail;
        this.cardThumbnail.style.display = thumbnail != null ? DisplayStyle.Flex : DisplayStyle.None;
        this.cardCollection.text = (level.CollectionDisplayName ?? string.Empty).ToUpperInvariant();
        this.cardTitle.text = level.DisplayName;
        this.cardMode.text = SceneFlow.ModeText(LevelManager.LevelManagerMode, LevelManager.NumPlayers);
        this.card.AddToClassList("sf-card--visible");
    }

    /// <summary>
    /// The loading card's mode line: the mode, and for a race with 2 or more cars the car count
    /// ("Race | 3 cars").
    /// </summary>
    public static string ModeText(LevelManagerMode mode, int cars)
    {
        return mode == LevelManagerMode.Race && cars > 1 ? $"Race | {cars} cars" : mode.ToString();
    }

    /// <summary>
    /// USS class that tints the cover for a mode; null for Exploration (charcoal).
    /// </summary>
    public static string ModeClass(LevelManagerMode mode)
    {
        return mode == LevelManagerMode.Autograder ? "sf--autograder"
            : mode == LevelManagerMode.Race ? "sf--race"
            : null;
    }

    /// <summary>
    /// Tints the cover and card for the mode the system is in when a change starts: the chosen
    /// mode from a menu, the level's mode from inside a level. The tint holds until the reveal ends.
    /// </summary>
    private void SetMode(LevelManagerMode mode)
    {
        foreach (LevelManagerMode each in (LevelManagerMode[])Enum.GetValues(typeof(LevelManagerMode)))
        {
            if (SceneFlow.ModeClass(each) is string name)
            {
                this.root.EnableInClassList(name, each == mode);
            }
        }
    }

    private void SetVisible(bool visible)
    {
        this.root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible)
        {
            this.wipe.Progress = 0;
            this.card.RemoveFromClassList("sf-card--visible");
        }
    }
}
