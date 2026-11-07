using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The screen shown during races with 2 to 4 cars (UI Toolkit, RaceScreen.uxml): the mode chip and
/// timer, one tile per car's view with its badge and speed, and the race camera in a free tile.
/// Clicking a car's view selects it for the camera keys and scroll wheel. Pause, messages, and the
/// result card match the single-car Hud.
/// </summary>
public class RaceScreen : ScreenManager
{
    #region Constants
    /// <summary>
    /// The most tiles on the screen (a 2 x 2 grid).
    /// </summary>
    public const int MaxTiles = 4;
    #endregion

    #region Set in Unity Editor
    /// <summary>
    /// The document showing RaceScreen.uxml.
    /// </summary>
    [SerializeField]
    private UIDocument document;

    /// <summary>
    /// RaceTile.uxml, instantiated once per car and once for the race camera.
    /// </summary>
    [SerializeField]
    private VisualTreeAsset tileTemplate;
    #endregion

    #region Public Interface
    /// <summary>
    /// The tile showing each car's view, by car index.
    /// </summary>
    public IReadOnlyList<VisualElement> CarTiles => this.carTiles;

    /// <summary>
    /// The tile showing the race camera, or null when every tile shows a car.
    /// </summary>
    public VisualElement OverviewTile { get; private set; }

    #region Overrides
    public override void ShowMessage(string message, Color color, float persistTime = -1, float fadeTime = 1.0f)
    {
        bool show = !string.IsNullOrEmpty(message);
        this.toast.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        this.toastText.text = message;
        this.toast.EnableInClassList("hud-toast--error", ScreenManager.IsError(color));
        this.toast.EnableInClassList("hud-toast--warning", ScreenManager.IsWarning(color));
        this.toastHideAt = show && persistTime > 0 ? Time.unscaledTime + persistTime + fadeTime : -1;
    }

    public override void UpdateTime(float mainTime, float[] keyPointDurations)
    {
        this.timer.Text = Hud.FormatLapTime(mainTime);
    }

    public override void SetPause(bool isPaused)
    {
        this.root.Q("pause").style.display = isPaused ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public override void HandleWin(float time, bool isNewBestTime)
    {
        Hud.ShowResult(this.root, MenuIcon.Glyph.Race, "Finished", Hud.FormatLapTime(time), isNewBestTime, false);
    }

    public override void HandleFailure(int carIndex, string reason)
    {
        // Races have no failure objectives
    }

    public override void UpdateConnectedPrograms(bool[] connectedPrograms)
    {
        for (int i = 0; i < this.carTiles.Count; i++)
        {
            bool isConnected = i < connectedPrograms.Length && connectedPrograms[i];
            this.carTiles[i].EnableInClassList("race-tile--waiting", !isConnected);
            this.carTiles[i].Q("connection").EnableInClassList("hud-connection--on", isConnected);
        }
    }

    public override void UpdateMode(SimulationMode mode)
    {
        (string label, MenuIcon.Glyph icon) = Hud.Modes[(int)mode];
        this.modeLabel.text = label;
        this.modeIcon.Icon = icon;
    }

    public override void UpdateTimeScale(float timeScale)
    {
        this.timeScaleLabel.text = timeScale >= 1 ? string.Empty : $"{timeScale.ToString("0.###", CultureInfo.InvariantCulture)}x";
    }
    #endregion

    /// <summary>
    /// Creates one tile per car view, and one for the race camera when a tile is free.
    /// </summary>
    /// <param name="carCameras">The textures to which each car-view camera renders, ordered by car index.</param>
    /// <param name="raceCamera">The texture to which the race cameras render.</param>
    public void SetCameras(Texture[] carCameras, Texture raceCamera)
    {
        VisualElement tiles = this.root.Q("tiles");
        tiles.Clear();
        this.carTiles.Clear();
        for (int i = 0; i < carCameras.Length && i < RaceScreen.MaxTiles; i++)
        {
            VisualElement tile = this.NewTile(carCameras[i]);
            tile.Q<Label>("car-label").text = $"Car {i + 1}";
            tile.Q("swatch").style.backgroundColor = SavedDataManager.Data.CarCustomizations[i].ShellColor.Color;
            tile.AddToClassList("race-tile--waiting");
            this.carTiles.Add(tile);
        }

        // With 2 cars the race camera fills the bottom middle; with 3, the fourth tile
        this.OverviewTile = null;
        if (this.carTiles.Count < RaceScreen.MaxTiles)
        {
            this.OverviewTile = this.NewTile(raceCamera);
            this.OverviewTile.AddToClassList("race-tile--overview");
        }

        // Two rows of up to two tiles; rows do not wrap, so the grid fits any window width
        List<VisualElement> ordered = new List<VisualElement>(this.carTiles);
        if (this.OverviewTile != null)
        {
            ordered.Add(this.OverviewTile);
        }
        for (int i = 0; i < ordered.Count; i += 2)
        {
            VisualElement row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("race-row");
            row.Add(ordered[i]);
            if (i + 1 < ordered.Count)
            {
                row.Add(ordered[i + 1]);
            }
            tiles.Add(row);
        }

        for (int i = 0; i < this.carTiles.Count; i++)
        {
            int car = i;
            this.carTiles[i].Q("view").RegisterCallback<ClickEvent>(click => this.Select(this.SelectedCar == car ? -1 : car));
        }
        this.Select(-1);
    }

    /// <summary>
    /// The car whose view takes Space, C, and the scroll wheel, or -1 for none.
    /// </summary>
    public int SelectedCar { get; private set; } = -1;

    /// <summary>
    /// Gives the camera keys and scroll wheel to one car's view (its tile outlined), or to none.
    /// </summary>
    /// <param name="car">The car index, or -1 for none.</param>
    public void Select(int car)
    {
        this.SelectedCar = car;
        for (int i = 0; i < this.carTiles.Count; i++)
        {
            this.carTiles[i].EnableInClassList("race-tile--selected", i == car);
            Racecar racecar = LevelManager.GetCar(i);
            if (racecar != null)
            {
                racecar.TakesViewInput = i == car;
            }
        }
    }
    #endregion

    private readonly List<VisualElement> carTiles = new List<VisualElement>();
    private VisualElement root;
    private VisualElement toast;
    private Label toastText;
    private ParallelogramElement timer;
    private MenuIcon modeIcon;
    private Label modeLabel;
    private Label timeScaleLabel;

    /// <summary>
    /// Unscaled time at which the toast hides, or -1 to keep it.
    /// </summary>
    private float toastHideAt = -1;

    private void Awake()
    {
        this.root = this.document.rootVisualElement;
        this.toast = this.root.Q("toast");
        this.toastText = this.root.Q<Label>("toast-text");
        this.timer = this.root.Q<ParallelogramElement>("timer");
        this.modeIcon = this.root.Q<MenuIcon>("mode-icon");
        this.modeLabel = this.root.Q<Label>("mode-label");
        this.timeScaleLabel = this.root.Q<Label>("time-scale");

        // Unity needs a camera rendering to the display; this one draws nothing behind the panel
        Camera blank = this.gameObject.AddComponent<Camera>();
        blank.cullingMask = 0;
        blank.clearFlags = CameraClearFlags.SolidColor;
        blank.backgroundColor = Color.black;

        this.ShowMessage(string.Empty, Color.white);
        this.UpdateMode(SimulationMode.DefaultDrive);
        this.UpdateTimeScale(1);
        this.UpdateTime(0, new float[0]);
        this.SetPause(false);
    }

    private void Update()
    {
        if (this.toastHideAt > 0 && Time.unscaledTime >= this.toastHideAt)
        {
            this.ShowMessage(string.Empty, Color.white);
        }

        for (int i = 0; i < this.carTiles.Count; i++)
        {
            Racecar car = LevelManager.GetCar(i);
            if (car == null)
            {
                continue;
            }
            this.carTiles[i].Q<SpeedGauge>("gauge").Speed = car.Physics.LinearVelocity.magnitude;
        }
    }

    private VisualElement NewTile(Texture view)
    {
        VisualElement tile = this.tileTemplate.Instantiate().Q("tile");
        tile.Q("view").style.backgroundImage = view is RenderTexture texture
            ? Background.FromRenderTexture(texture)
            : Background.FromTexture2D(view as Texture2D);
        return tile;
    }
}
