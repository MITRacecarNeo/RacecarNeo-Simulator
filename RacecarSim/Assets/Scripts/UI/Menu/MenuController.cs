using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The main menu (MainMenu.uxml) over the car turntable: Home; Map select, where a level card, a
/// mode, and a car count start a level; Settings, previewing the shell colors on the turntable car;
/// Best times; Controls, listed from InputBindings; and a dialog for the first-autograder username
/// reminder and the saved-data reset notice; and the splash on the first appearance in a run
/// (MenuSplash). Also handles a level requested on the command line and the Konami Code.
/// </summary>
public class MenuController : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The menu document (MainMenu.uxml on the shared panel settings).
    /// </summary>
    [SerializeField]
    private UIDocument document;

    /// <summary>
    /// The car turntable, which previews shell colors while Settings is open.
    /// </summary>
    [SerializeField]
    private MenuShowcase showcase;
    #endregion

    #region Constants
    private const string openClass = "mn-screen--open";

    /// <summary>
    /// Narrowest Controls column in panel pixels; the width decides how many columns fit (1 to 3).
    /// </summary>
    private const float controlColumnWidth = 620;

    /// <summary>
    /// Controls screen scroll speed for the arrow keys and stick, in panel pixels per second.
    /// </summary>
    private const float controlScrollSpeed = 900;

    /// <summary>
    /// Seconds the Clear button on Best times waits for a second press.
    /// </summary>
    private const float clearConfirmSeconds = 3;

    private const string saveResetMessage =
        "This version stores best times in a new format. Best times and car colors saved by earlier versions have been reset.";
    #endregion

    /// <summary>
    /// The level, mode, and car count chosen last, kept across returns to the menu.
    /// </summary>
    private static LevelInfo lastLevel;
    private static LevelManagerMode lastMode = LevelManagerMode.Exploration;
    private static int lastCars = 1;

    private VisualElement root;
    private VisualElement[] screens;
    private VisualElement home;
    private VisualElement mapSelect;
    private VisualElement settings;
    private VisualElement bestTimes;
    private VisualElement controls;
    private VisualElement connect;
    private VisualElement dialog;
    private TextField connectHost;
    private TextField connectBridgePort;
    private TextField connectApiPort;
    private Label connectStatus;
    private Button connectGo;
    private Button playButton;

    private VisualElement tabs;
    private ScrollView cards;
    private VisualElement detailThumbnail;
    private Label detailCollection;
    private Label detailTitle;
    private VisualElement modes;
    private VisualElement carsRow;
    private Label carsCount;
    private readonly Dictionary<LevelInfo, Button> cardButtons = new Dictionary<LevelInfo, Button>();

    private LevelCollection collection;

    /// <summary>
    /// The map select tabs: the built-in collections, then Mapped Worlds when there are any.
    /// </summary>
    private List<LevelCollection> collections = new List<LevelCollection>(LevelCollection.LevelCollections);
    private LevelInfo level;

    /// <summary>
    /// Wall heights and speck filters offered for a mapped world (meters; smallest kept group in
    /// cells, 1 keeping everything), with their labels.
    /// </summary>
    private static readonly (double Value, string Label)[] wallHeights = { (0.3, "0.3 m"), (0.5, "0.5 m"), (1.0, "1.0 m"), (2.0, "2.0 m") };
    private static readonly (int Value, string Label)[] speckSizes = { (1, "Off"), (2, "1 cell"), (5, "4 cells") };
    private LevelManagerMode mode;
    private int cars;

    private SettingsDraft draft;
    private int editedCar;

    /// <summary>
    /// How the shell color is picked: 0 sliders, 1 presets, 2 hex code.
    /// </summary>
    private int shellPick;
    private bool isLoadingDraft;

    private Button clearButton;
    private float clearArmedUntil = -1;

    private readonly List<VisualElement> controlSections = new List<VisualElement>();
    private int controlColumns;
    private ScrollView controlsScroll;

    private Action dialogOk;
    private Action dialogOther;
    private int konamiIndex;

    #region Public Interface
    /// <summary>
    /// Whether the Connect tile on Home opens Connect car; grayed out while false. The
    /// -racecarsim-car launch option connects either way.
    /// </summary>
    public static bool IsConnectCarEnabled { get; set; }

    /// <summary>
    /// Time in seconds as the menu shows it: s.cc under a minute, m:ss.cc above.
    /// </summary>
    public static string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60);
        float rest = seconds - minutes * 60;
        return minutes > 0
            ? $"{minutes}:{rest.ToString("00.00", CultureInfo.InvariantCulture)}"
            : rest.ToString("0.00", CultureInfo.InvariantCulture);
    }
    #endregion

    private void Start()
    {
        this.root = this.document.rootVisualElement;
        this.home = this.root.Q("home");
        this.mapSelect = this.root.Q("map-select");
        this.settings = this.root.Q("settings-screen");
        this.bestTimes = this.root.Q("best-times-screen");
        this.controls = this.root.Q("controls-screen");
        this.connect = this.root.Q("connect-screen");
        this.dialog = this.root.Q("dialog");
        this.screens = new[] { this.home, this.mapSelect, this.settings, this.bestTimes, this.controls, this.connect };
        this.playButton = this.root.Q<Button>("play");

        this.root.Q<Label>("version").text = $"v{Application.version}";
        this.playButton.clicked += this.OpenMapSelect;
        this.root.Q<Button>("best-times").clicked += this.OpenBestTimes;
        this.root.Q<Button>("settings").clicked += this.OpenSettings;
        this.root.Q<Button>("controls").clicked += () => this.Open(this.controls, this.root.Q<Button>("controls-back"));
        this.root.Q<Button>("exit").clicked += () => Application.Quit(0);
        this.root.Q<Button>("connect").clicked += () => this.OpenConnect(null);
        this.root.Q<Button>("connect").SetEnabled(MenuController.IsConnectCarEnabled);
        this.root.Q<Button>("connect-back").clicked += this.CloseConnect;
        this.root.Q<Button>("connect-cancel").clicked += this.CloseConnect;
        this.connectGo = this.root.Q<Button>("connect-go");
        this.connectGo.clicked += this.BeginConnect;
        this.connectHost = this.root.Q<TextField>("connect-host");
        this.connectBridgePort = this.root.Q<TextField>("connect-bridge-port");
        this.connectApiPort = this.root.Q<TextField>("connect-api-port");
        this.connectStatus = this.root.Q<Label>("connect-status");
        this.root.Q<Button>("back").clicked += this.OpenHome;
        this.root.Q<Button>("best-times-back").clicked += this.OpenHome;
        this.root.Q<Button>("controls-back").clicked += this.OpenHome;
        this.root.Q<Button>("cars-less").clicked += () => this.SetCars(this.cars - 1);
        this.root.Q<Button>("cars-more").clicked += () => this.SetCars(this.cars + 1);
        this.root.Q<Button>("start").clicked += this.StartLevel;
        this.BuildWorldOptions();
        this.root.Q<Button>("dialog-ok").clicked += () => this.CloseDialog(this.dialogOk);
        this.root.Q<Button>("dialog-other").clicked += () => this.CloseDialog(this.dialogOther);

        this.cards = this.root.Q<ScrollView>("cards");
        this.tabs = this.root.Q("tabs");
        this.detailThumbnail = this.root.Q("detail-thumbnail");
        this.detailCollection = this.root.Q<Label>("detail-collection");
        this.detailTitle = this.root.Q<Label>("detail-title");
        this.modes = this.root.Q("modes");
        this.carsRow = this.root.Q("cars");
        this.carsCount = this.root.Q<Label>("cars-count");
        foreach (ScrollView grid in new[] { this.cards, this.root.Q<ScrollView>("best-times-cards") })
        {
            grid.contentContainer.style.flexDirection = FlexDirection.Row;
            grid.contentContainer.style.flexWrap = Wrap.Wrap;
        }
        foreach (ScrollView scroll in new[] { this.cards, this.root.Q<ScrollView>("best-times-cards"), this.root.Q<ScrollView>("settings-rows") })
        {
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        this.BuildModeTiles();
        this.BuildSettings();
        this.BuildControls();
        this.clearButton = this.root.Q<Button>("best-times-clear");
        this.clearButton.clicked += this.ClearBestTimes;

        this.OpenHome();

        VisualElement splash = this.root.Q("splash");
        if (MenuSplash.ShouldPlay)
        {
            this.StartCoroutine(this.PlaySplash(splash));
        }

        if (SavedDataManager.WasLegacyDataReset)
        {
            SavedDataManager.WasLegacyDataReset = false;
            Debug.Log("Save reset notice shown.");
            this.ShowDialog("Saved data reset", MenuController.saveResetMessage, null, "OK", null, null, null);
        }

        // A level requested on the command line skips the menu once, always on the level's own scene
        if (LaunchOptions.TryTakeLevelRequest(out LevelInfo requested))
        {
            this.StartRequestedLevel(requested);
        }
        else if (LaunchOptions.TryTakeWorldRequest(out string worldRequest))
        {
            CapturedWorld world = WorldLevels.Find(worldRequest, WorldLibrary.Default);
            if (world == null)
            {
                Debug.LogError($"Unknown world [{worldRequest}] passed to -racecarsim-world.");
            }
            else
            {
                SceneFlow.LoadLevel(WorldLevels.Prepare(world));
            }
        }
        else if (LaunchOptions.TryTakeCarRequest(out CarLinkTarget car))
        {
            this.OpenConnect(car);
            this.BeginConnect();
        }
    }

    private void Update()
    {
        if (this.clearArmedUntil > 0 && Time.unscaledTime > this.clearArmedUntil)
        {
            this.clearArmedUntil = -1;
            this.clearButton.text = "Clear";
        }

        // Keyboard 2 doubles as gamepad B, so typing in a text field must not go back
        bool back = Input.GetKeyDown(KeyCode.Escape) || (!this.IsTyping() && Controller.WasPressed(Controller.Button.B));
        this.UpdateConnect();
        if (this.dialog.ClassListContains("mn-dialog-backdrop--open"))
        {
            if (back)
            {
                this.CloseDialog(this.dialogOther ?? this.dialogOk);
            }
        }
        else if (this.mapSelect.ClassListContains(MenuController.openClass))
        {
            if (back)
            {
                this.OpenHome();
            }
            else if (Input.GetKeyDown(KeyCode.Q) || Controller.WasPressed(Controller.Button.LB))
            {
                this.StepCollection(-1);
            }
            else if (Input.GetKeyDown(KeyCode.E) || Controller.WasPressed(Controller.Button.RB))
            {
                this.StepCollection(1);
            }
        }
        else if (this.settings.ClassListContains(MenuController.openClass))
        {
            if (back)
            {
                this.CloseSettings(false);
            }
        }
        else if (this.connect.ClassListContains(MenuController.openClass))
        {
            if (back)
            {
                this.CloseConnect();
            }
        }
        else if (back && !this.home.ClassListContains(MenuController.openClass))
        {
            this.OpenHome();
        }
        else if (this.controls.ClassListContains(MenuController.openClass))
        {
            // Rows are not focusable, so the keys and the stick scroll the list
            float direction = Controller.GetJoystick(Controller.Joystick.LEFT).y
                + (Input.GetKey(KeyCode.DownArrow) ? -1 : 0) + (Input.GetKey(KeyCode.UpArrow) ? 1 : 0);
            if (Mathf.Abs(direction) > 0.2f)
            {
                Vector2 offset = this.controlsScroll.scrollOffset;
                offset.y -= Mathf.Clamp(direction, -1, 1) * MenuController.controlScrollSpeed * Time.unscaledDeltaTime;
                this.controlsScroll.scrollOffset = offset;
            }
        }

        if (Input.anyKeyDown)
        {
            KeyCode? pressed = KonamiCode.Keys.Distinct().Where(Input.GetKeyDown).Select(key => (KeyCode?)key).FirstOrDefault();
            this.konamiIndex = pressed.HasValue ? KonamiCode.NextIndex(this.konamiIndex, pressed.Value) : 0;
            if (this.konamiIndex == KonamiCode.Keys.Length)
            {
                Debug.Log("Cheat mode activated");
                Settings.CheatMode = true;
                this.konamiIndex = 0;
            }
        }
    }

    /// <summary>
    /// True while a text field has keyboard focus.
    /// </summary>
    private bool IsTyping()
    {
        VisualElement focused = this.root?.focusController?.focusedElement as VisualElement;
        return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
    }

    /// <summary>
    /// Opens Connect car, with a car's address filled in, else the last car used.
    /// </summary>
    private void OpenConnect(CarLinkTarget target)
    {
        CarLinkTarget shown = target
            ?? (SavedDataManager.Data.RecentCars.Count > 0 ? SavedDataManager.Data.RecentCars[0] : new CarLinkTarget());
        this.FillConnect(shown);
        this.connectStatus.text = string.Empty;
        this.connectGo.SetEnabled(true);
        this.BuildRecentCars();
        this.Open(this.connect, this.connectGo);
    }

    private void FillConnect(CarLinkTarget target)
    {
        this.connectHost.SetValueWithoutNotify(target.Host);
        this.connectBridgePort.SetValueWithoutNotify(target.BridgePort.ToString(CultureInfo.InvariantCulture));
        this.connectApiPort.SetValueWithoutNotify(target.ApiPort.ToString(CultureInfo.InvariantCulture));
    }

    private void BuildRecentCars()
    {
        VisualElement list = this.root.Q("connect-recent");
        list.Clear();
        foreach (CarLinkTarget recent in SavedDataManager.Data.RecentCars)
        {
            CarLinkTarget car = recent;
            string label = string.IsNullOrEmpty(car.Name) ? car.Label : $"{car.Name} ({car.Label})";
            Button button = new Button(() =>
            {
                this.FillConnect(car);
                this.BeginConnect();
            })
            { text = label };
            button.AddToClassList("rcn-button");
            button.AddToClassList("rcn-button--secondary");
            button.AddToClassList("mn-recent-car");
            list.Add(button);
        }
        bool any = SavedDataManager.Data.RecentCars.Count > 0;
        this.root.Q("connect-recent-title").style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
        list.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Starts connecting to the car in the fields; the result shows in UpdateConnect.
    /// </summary>
    private void BeginConnect()
    {
        string host = this.connectHost.value.Trim();
        if (!CarLinkTarget.IsValidHost(host))
        {
            this.connectStatus.text = "Enter the car's address.";
            return;
        }
        if (!CarLinkTarget.TryParsePort(this.connectBridgePort.value.Trim(), out int bridgePort)
            || !CarLinkTarget.TryParsePort(this.connectApiPort.value.Trim(), out int apiPort))
        {
            this.connectStatus.text = "Ports are numbers from 1 to 65535.";
            return;
        }
        CarLinkSession.Begin(new CarLinkTarget { Host = host, BridgePort = bridgePort, ApiPort = apiPort });
        this.connectStatus.text = "Connecting...";
        this.connectGo.SetEnabled(false);
    }

    /// <summary>
    /// Follows a connection started from Connect car: loads the car world once connected, or
    /// shows why it failed.
    /// </summary>
    private void UpdateConnect()
    {
        CarLinkSession session = CarLinkSession.Current;
        if (session == null || !this.connect.ClassListContains(MenuController.openClass))
        {
            return;
        }
        if (session.Status == CarLinkStatus.Connected && !SceneFlow.IsTransitioning)
        {
            SavedDataManager.Data.RememberCar(session.Target);
            SavedDataManager.Save();
            this.connectStatus.text = "Connected.";
            SceneFlow.Load(CarWorld.SceneName, SceneTransition.Wipe);
        }
        else if (session.Status == CarLinkStatus.Failed)
        {
            this.connectStatus.text = session.FailureMessage;
            session.Close();
            this.connectGo.SetEnabled(true);
        }
    }

    private void CloseConnect()
    {
        CarLinkSession session = CarLinkSession.Current;
        if (session != null && session.Status != CarLinkStatus.Connected)
        {
            session.Close();
        }
        this.OpenHome();
    }

    /// <summary>
    /// Runs the splash with keyboard focus on it, so the key that skips it does not also press
    /// the focused menu button, then gives focus back to Play.
    /// </summary>
    private System.Collections.IEnumerator PlaySplash(VisualElement splash)
    {
        splash.focusable = true;
        splash.Focus();
        yield return MenuSplash.Play(splash, this.showcase);
        splash.focusable = false;
        if (!this.dialog.ClassListContains("mn-dialog-backdrop--open"))
        {
            this.playButton.Focus();
        }
    }

    private void Open(VisualElement screen, Focusable focus)
    {
        foreach (VisualElement each in this.screens)
        {
            each.EnableInClassList(MenuController.openClass, each == screen);
        }
        focus?.Focus();
    }

    private void OpenHome()
    {
        this.Open(this.home, this.playButton);
    }

    #region Map select
    private void OpenMapSelect()
    {
        // Worlds are read each time, so a world saved in the car world shows up
        this.collections = new List<LevelCollection>(LevelCollection.LevelCollections);
        this.collections.Add(WorldLevels.Collection(WorldLibrary.Default));
        this.BuildTabs();
        this.Open(this.mapSelect, null);
        LevelCollection holding = this.collections.FirstOrDefault(each => each.Levels.Contains(MenuController.lastLevel)) ?? this.collections[0];
        this.ShowCollection(holding, holding.Levels.Contains(MenuController.lastLevel) ? MenuController.lastLevel : holding.Levels.FirstOrDefault());
    }

    private void BuildTabs()
    {
        this.tabs.Clear();
        foreach (LevelCollection each in this.collections)
        {
            Button tab = new Button(() => this.ShowCollection(each, each.Levels.FirstOrDefault())) { text = each.DisplayName };
            tab.AddToClassList("mn-tab");
            tab.userData = each;
            this.tabs.Add(tab);
        }
    }

    private void BuildModeTiles()
    {
        foreach (LevelManagerMode each in (LevelManagerMode[])Enum.GetValues(typeof(LevelManagerMode)))
        {
            Button tile = new Button(() => this.SetMode(each));
            tile.AddToClassList("mn-mode");
            tile.userData = each;
            MenuIcon icon = new MenuIcon { Icon = each == LevelManagerMode.Autograder ? MenuIcon.Glyph.Autograder : each == LevelManagerMode.Race ? MenuIcon.Glyph.Race : MenuIcon.Glyph.Exploration };
            icon.AddToClassList("mn-mode-icon");
            tile.Add(icon);
            Label label = new Label(each.ToString());
            label.AddToClassList("rcn-label");
            tile.Add(label);
            this.modes.Add(tile);
        }
    }

    private void StepCollection(int step)
    {
        int index = (this.collections.IndexOf(this.collection) + step + this.collections.Count) % this.collections.Count;
        this.ShowCollection(this.collections[index], this.collections[index].Levels.FirstOrDefault());
    }

    private void ShowCollection(LevelCollection shown, LevelInfo select)
    {
        this.collection = shown;
        foreach (Button tab in this.tabs.Children().OfType<Button>())
        {
            tab.EnableInClassList("mn-tab--selected", tab.userData == shown);
        }

        this.cards.Clear();
        this.cardButtons.Clear();
        foreach (LevelInfo each in shown.Levels)
        {
            Button card = MenuController.LevelCard(each, () => this.root.Q<Button>("start").Focus());
            card.RegisterCallback<FocusInEvent>(_ => this.SelectLevel(each));
            this.cards.Add(card);
            this.cardButtons[each] = card;
        }

        // Mapped Worlds ends with an import card, and may hold nothing else
        Button import = null;
        if (shown.DisplayName == WorldLevels.CollectionDisplayName)
        {
            import = MenuController.ImportCard(this.OpenImport);
            this.cards.Add(import);
        }
        if (select == null)
        {
            this.SelectLevel(null);
            import?.Focus();
            return;
        }
        this.SelectLevel(select);
        this.cardButtons[select].Focus();
        this.cards.ScrollTo(this.cardButtons[select]);
    }

    /// <summary>
    /// The card that imports a map into Mapped Worlds.
    /// </summary>
    private static Button ImportCard(Action clicked)
    {
        Button card = new Button(clicked);
        card.AddToClassList("mn-card");
        VisualElement face = new VisualElement();
        face.AddToClassList("mn-card-thumbnail");
        face.AddToClassList("mn-card-import");
        MenuIcon icon = new MenuIcon { Icon = MenuIcon.Glyph.Plus };
        icon.AddToClassList("mn-card-import-icon");
        face.Add(icon);
        card.Add(face);
        VisualElement footer = new VisualElement();
        footer.AddToClassList("mn-card-footer");
        Label name = new Label("Import map");
        name.AddToClassList("rcn-label");
        name.AddToClassList("mn-card-name");
        footer.Add(name);
        card.Add(footer);
        return card;
    }

    private void OpenImport()
    {
        this.ShowDialog(
            "Import map",
            "Type the path to a map's .yaml file. Make sure that its .pgm image is in the same folder.",
            string.Empty,
            "Import",
            () =>
            {
                string path = this.root.Q<TextField>("dialog-input").value.Trim().Trim('"');
                try
                {
                    CapturedWorld imported = WorldLibrary.Default.Import(path);
                    MenuController.lastLevel = WorldLevels.LevelFor(imported);
                    this.OpenMapSelect();
                }
                catch (Exception exception) when (exception is System.IO.IOException || exception is CarLinkFormatException
                    || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
                {
                    Debug.LogWarning($"Worlds: import of {path} failed: {exception.Message}");
                    this.ShowDialog("Import failed", $"The map could not be read: {exception.Message}", null, "OK", this.FocusMapSelect, null, null);
                }
            },
            "Cancel",
            this.FocusMapSelect);
    }

    /// <summary>
    /// A level card: thumbnail, then the name and the best time when there is one.
    /// </summary>
    private static Button LevelCard(LevelInfo shown, Action clicked)
    {
        Button card = new Button(clicked);
        card.AddToClassList("mn-card");

        VisualElement thumbnail = new VisualElement();
        thumbnail.AddToClassList("mn-card-thumbnail");
        thumbnail.style.backgroundImage = LevelPresentationCatalog.Get(shown).Thumbnail;
        card.Add(thumbnail);

        VisualElement footer = new VisualElement();
        footer.AddToClassList("mn-card-footer");
        Label name = new Label(shown.DisplayName);
        name.AddToClassList("rcn-label");
        name.AddToClassList("mn-card-name");
        footer.Add(name);

        BestTimeInfo best = shown.IsRaceable ? SavedDataManager.Data.GetBestTimes(shown) : null;
        if (best != null && best.OverallTime < float.MaxValue)
        {
            VisualElement badge = new VisualElement();
            badge.AddToClassList("mn-card-best");
            MenuIcon icon = new MenuIcon { Icon = MenuIcon.Glyph.BestTimes };
            icon.AddToClassList("mn-card-best-icon");
            badge.Add(icon);
            Label time = new Label(MenuController.FormatTime(best.OverallTime));
            time.AddToClassList("rcn-numeric");
            time.AddToClassList("mn-card-best-time");
            badge.Add(time);
            footer.Add(badge);
        }
        card.Add(footer);
        return card;
    }

    private void SelectLevel(LevelInfo selected)
    {
        this.level = selected;
        CapturedWorld world = WorldLevels.WorldOf(selected);
        this.root.Q("world-options").style.display = world != null ? DisplayStyle.Flex : DisplayStyle.None;
        this.RefreshWorldOptions();
        this.root.Q<Button>("start").SetEnabled(selected != null);
        // A mapped world runs in Exploration only; its settings take the mode tiles' place
        this.modes.style.display = selected != null && world == null ? DisplayStyle.Flex : DisplayStyle.None;
        if (selected == null)
        {
            // An empty Mapped Worlds: only the import card
            this.detailThumbnail.style.backgroundImage = StyleKeyword.None;
            this.detailCollection.text = this.collection.DisplayName.ToUpperInvariant();
            this.detailTitle.text = this.collection.DisplayName;
            this.carsRow.style.display = DisplayStyle.None;
            return;
        }
        foreach (KeyValuePair<LevelInfo, Button> each in this.cardButtons)
        {
            each.Value.EnableInClassList("mn-card--selected", each.Key == selected);
        }

        this.detailThumbnail.style.backgroundImage = LevelPresentationCatalog.Get(selected).Thumbnail;
        this.detailCollection.text = this.collection.DisplayName.ToUpperInvariant();
        this.detailTitle.text = selected.DisplayName;
        foreach (Button tile in this.modes.Children().OfType<Button>())
        {
            tile.SetEnabled(selected.SupportedModes.Contains((LevelManagerMode)tile.userData));
        }
        this.carsRow.style.display = selected.MaxCars > 1 ? DisplayStyle.Flex : DisplayStyle.None;

        LevelManagerMode preferred = selected == MenuController.lastLevel ? MenuController.lastMode : this.mode;
        this.SetMode(selected.SupportedModes.Contains(preferred) ? preferred : selected.SupportedModes[0]);
        this.SetCars(selected == MenuController.lastLevel ? MenuController.lastCars : 1);
    }

    private void SetMode(LevelManagerMode chosen)
    {
        this.mode = chosen;
        foreach (Button tile in this.modes.Children().OfType<Button>())
        {
            tile.EnableInClassList("mn-mode--selected", (LevelManagerMode)tile.userData == chosen);
        }
        if (chosen != LevelManagerMode.Race && this.cars > 1)
        {
            this.SetCars(1);
        }
    }

    private void SetCars(int count)
    {
        this.cars = Mathf.Clamp(count, 1, this.level.MaxCars);
        this.carsCount.text = this.cars.ToString(CultureInfo.InvariantCulture);

        // More than one car is a race, as in the earlier menu
        if (this.cars > 1 && this.mode != LevelManagerMode.Race && this.level.SupportedModes.Contains(LevelManagerMode.Race))
        {
            this.SetMode(LevelManagerMode.Race);
        }
    }

    private void StartLevel()
    {
        if (this.level == null)
        {
            return;
        }
        MenuController.lastLevel = this.level;
        MenuController.lastMode = this.mode;
        MenuController.lastCars = this.cars;
        CapturedWorld world = WorldLevels.WorldOf(this.level);
        int buildIndex = world != null ? WorldLevels.Prepare(world) : LevelLaunch.Prepare(this.level, this.mode, this.cars);

        if (LevelLaunch.NeedsUsernameReminder(this.mode))
        {
            this.ShowDialog(
                "Course ID",
                "If you are taking the BWSI RACECAR Neo course on Edly, enter the course ID shown on the lab's "
                    + "autograder problem (ex: U8XPVVPH). Otherwise, score codes will not be valid.",
                Settings.Username,
                "Save",
                () =>
                {
                    Settings.Username = SettingsDraft.CleanUsername(this.root.Q<TextField>("dialog-input").value);
                    Settings.SaveSettings();
                    SceneFlow.LoadLevel(buildIndex);
                },
                "Keep default",
                () => SceneFlow.LoadLevel(buildIndex));
            return;
        }
        SceneFlow.LoadLevel(buildIndex);
    }

    private void StartRequestedLevel(LevelInfo requested)
    {
        if (!string.IsNullOrEmpty(LaunchOptions.Current.Username))
        {
            Settings.Username = LaunchOptions.Current.Username;
        }
        if (!requested.SupportedModes.Contains(LaunchOptions.Current.Mode))
        {
            Debug.LogError($"Level [{requested.DisplayName}] does not support {LaunchOptions.Current.Mode} mode (-racecarsim-mode).");
            return;
        }
        LevelManager.LevelInfo = requested;
        LevelManager.LevelManagerMode = LaunchOptions.Current.Mode;
        LevelManager.NumPlayers = Mathf.Clamp(LaunchOptions.Current.NumCars, 1, requested.MaxCars);
        SceneFlow.LoadLevel(LevelManager.LevelManagerMode == LevelManagerMode.Autograder ? requested.AutograderBuildIndex : requested.BuildIndex);
    }
    /// <summary>
    /// Wall height, speck filter, and closed edges for the selected mapped world, saved to its
    /// world.json on each change; and Delete world.
    /// </summary>
    private void BuildWorldOptions()
    {
        VisualElement walls = this.root.Q("world-walls");
        foreach ((double value, string label) in MenuController.wallHeights)
        {
            Button segment = new Button(() => this.ChangeWorld(world => world.WallHeight = value)) { text = label, userData = value };
            segment.AddToClassList("mn-segment");
            walls.Add(segment);
        }
        VisualElement specks = this.root.Q("world-specks");
        foreach ((int value, string label) in MenuController.speckSizes)
        {
            Button segment = new Button(() => this.ChangeWorld(world => world.SpeckleCells = value)) { text = label, userData = value };
            segment.AddToClassList("mn-segment");
            specks.Add(segment);
        }
        MenuController.SwitchRow(
            this.root.Q("world-close"),
            "Close open edges",
            () => WorldLevels.WorldOf(this.level)?.Unknown == CapturedWorld.UnknownClosed,
            value => this.ChangeWorld(world => world.Unknown = value ? CapturedWorld.UnknownClosed : CapturedWorld.UnknownOpen));
        this.root.Q<Button>("world-delete").clicked += this.ConfirmDeleteWorld;
    }

    private void ChangeWorld(Action<CapturedWorld> change)
    {
        CapturedWorld world = WorldLevels.WorldOf(this.level);
        if (world == null)
        {
            return;
        }
        change(world);
        try
        {
            WorldLibrary.SaveSettings(world);
        }
        catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning($"Worlds: settings for {world.Name} not saved: {exception.Message}");
        }
        LevelPresentationCatalog.Register(new LevelPresentation { Key = this.level.Key, Thumbnail = WorldLevels.Thumbnail(world) });
        this.detailThumbnail.style.backgroundImage = LevelPresentationCatalog.Get(this.level).Thumbnail;
        this.RefreshWorldOptions();
    }

    private void RefreshWorldOptions()
    {
        CapturedWorld world = WorldLevels.WorldOf(this.level);
        if (world == null)
        {
            return;
        }
        foreach (Button segment in this.root.Q("world-walls").Children().OfType<Button>())
        {
            segment.EnableInClassList("mn-segment--selected", System.Math.Abs((double)segment.userData - world.WallHeight) < 1e-6);
        }
        foreach (Button segment in this.root.Q("world-specks").Children().OfType<Button>())
        {
            segment.EnableInClassList("mn-segment--selected", (int)segment.userData == world.SpeckleCells);
        }
        Button close = this.root.Q("world-close").Q<Button>();
        close.EnableInClassList("mn-switch--on", world.Unknown == CapturedWorld.UnknownClosed);
    }

    private void ConfirmDeleteWorld()
    {
        CapturedWorld world = WorldLevels.WorldOf(this.level);
        if (world == null)
        {
            return;
        }
        this.ShowDialog(
            "Delete world",
            $"Delete {world.Name}? This cannot be undone.",
            null,
            "Delete",
            () =>
            {
                try
                {
                    WorldLibrary.Default.Delete(world);
                }
                catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException)
                {
                    Debug.LogWarning($"Worlds: {world.Name} not deleted: {exception.Message}");
                }
                MenuController.lastLevel = null;
                this.OpenMapSelect();
                this.ShowCollection(this.collections[this.collections.Count - 1], this.collections[this.collections.Count - 1].Levels.FirstOrDefault());
            },
            "Cancel",
            this.FocusMapSelect);
    }

    private void FocusMapSelect()
    {
        if (this.level != null && this.cardButtons.TryGetValue(this.level, out Button card))
        {
            card.Focus();
        }
    }
    #endregion

    #region Settings
    private void BuildSettings()
    {
        MenuController.SwitchRow(this.root.Q("realism"), "Sensor noise", () => this.draft.IsRealism, value => this.draft.IsRealism = value);
        MenuController.SwitchRow(this.root.Q("hide-cars"), "Hide other cars in camera", () => this.draft.HideCarsInColorCamera, value => this.draft.HideCarsInColorCamera = value);
        MenuController.SwitchRow(this.root.Q("car-response"), "Real car response", () => this.draft.IsRealCarResponse, value => this.draft.IsRealCarResponse = value);
        MenuController.SwitchRow(this.root.Q("antenna"), "Remove antenna collider", () => this.draft.IsAntennaColliderRemoved, value => this.draft.IsAntennaColliderRemoved = value);
        MenuController.SwitchRow(this.root.Q("battery"), "Car stops when battery runs out", () => this.draft.IsBatteryMode, value => this.draft.IsBatteryMode = value);
        MenuController.SwitchRow(this.root.Q("dot-matrix"), "Dot matrix on HUD", () => this.draft.ShowDotMatrix, value => this.draft.ShowDotMatrix = value);
        MenuController.SwitchRow(this.root.Q("shiny"), "Shiny shell", () => this.draft.IsShellShiny[this.editedCar], value =>
        {
            this.draft.IsShellShiny[this.editedCar] = value;
            this.PreviewShell();
        });

        VisualElement depth = this.root.Q("depth");
        foreach (Settings.DepthResolution each in (Settings.DepthResolution[])Enum.GetValues(typeof(Settings.DepthResolution)))
        {
            Button segment = new Button(() =>
            {
                this.draft.DepthRes = each;
                this.RefreshSettings();
            }) { text = each.ToString() };
            segment.AddToClassList("mn-segment");
            segment.userData = each;
            depth.Add(segment);
        }

        VisualElement carDetail = this.root.Q("car-detail");
        foreach (Settings.CarDetailLevel each in (Settings.CarDetailLevel[])Enum.GetValues(typeof(Settings.CarDetailLevel)))
        {
            Button segment = new Button(() =>
            {
                this.draft.CarDetail = each;
                this.RefreshSettings();
            }) { text = each.ToString() };
            segment.AddToClassList("mn-segment");
            segment.userData = each;
            carDetail.Add(segment);
        }

        // One swatch per car, numbered as the cars are in a race; not presets
        VisualElement swatches = this.root.Q("cars-swatches");
        for (int i = 0; i < SavedDataManager.Data.CarCustomizations.Length; i++)
        {
            int car = i;
            Button swatch = new Button(() =>
            {
                this.editedCar = car;
                this.RefreshSettings();
            });
            swatch.AddToClassList("mn-swatch");
            Label number = new Label((i + 1).ToString(CultureInfo.InvariantCulture)) { pickingMode = PickingMode.Ignore };
            number.AddToClassList("mn-swatch-number");
            swatch.Add(number);
            swatches.Add(swatch);
        }

        VisualElement pick = this.root.Q("shell-pick");
        string[] pickNames = { "Sliders", "Presets", "Code" };
        for (int i = 0; i < pickNames.Length; i++)
        {
            int mode = i;
            Button segment = new Button(() =>
            {
                this.shellPick = mode;
                this.RefreshSettings();
            }) { text = pickNames[i], userData = mode };
            segment.AddToClassList("mn-segment");
            pick.Add(segment);
        }

        VisualElement presets = this.root.Q("shell-presets");
        foreach (Color preset in MenuController.ShellPresets)
        {
            Button button = new Button(() =>
            {
                this.draft.ShellColors[this.editedCar] = preset;
                this.RefreshSettings();
            }) { userData = preset };
            button.AddToClassList("mn-preset");
            button.style.backgroundColor = preset;
            presets.Add(button);
        }

        this.root.Q<TextField>("shell-code").RegisterValueChangedCallback(change =>
        {
            if (!this.isLoadingDraft && MenuController.TryParseShellCode(change.newValue, out Color color))
            {
                this.draft.ShellColors[this.editedCar] = color;
                this.RefreshSettings();
            }
        });

        foreach ((string name, int channel) in new[] { ("shell-r", 0), ("shell-g", 1), ("shell-b", 2) })
        {
            this.root.Q<Slider>(name).RegisterValueChangedCallback(change =>
            {
                if (this.isLoadingDraft)
                {
                    return;
                }
                Color color = this.draft.ShellColors[this.editedCar];
                color[channel] = change.newValue;
                this.draft.ShellColors[this.editedCar] = color;
                this.RefreshSettings();
            });
        }

        this.root.Q<TextField>("username").RegisterValueChangedCallback(change => this.draft.Username = change.newValue);
        this.root.Q<Button>("settings-back").clicked += () => this.CloseSettings(false);
        this.root.Q<Button>("settings-cancel").clicked += () => this.CloseSettings(false);
        this.root.Q<Button>("settings-save").clicked += () => this.CloseSettings(true);
        this.root.Q<Button>("settings-defaults").clicked += () =>
        {
            this.draft.RestoreDefaults();
            this.RefreshSettings();
        };
    }

    private void OpenSettings()
    {
        this.draft = SettingsDraft.Current();
        this.editedCar = 0;
        this.Open(this.settings, this.root.Q("realism").Q<Button>());
        this.RefreshSettings();
    }

    private void CloseSettings(bool save)
    {
        if (save)
        {
            this.draft.Apply();
        }
        this.showcase.ClearPreview();
        this.OpenHome();
    }

    /// <summary>
    /// Shows the draft in every control and previews the edited car's shell on the turntable.
    /// </summary>
    private void RefreshSettings()
    {
        this.isLoadingDraft = true;
        foreach (string row in new[] { "realism", "hide-cars", "car-response", "antenna", "battery", "dot-matrix", "shiny" })
        {
            Button toggle = this.root.Q(row).Q<Button>();
            toggle.EnableInClassList("mn-switch--on", ((Func<bool>)toggle.userData)());
        }
        foreach (Button segment in this.root.Q("depth").Children().OfType<Button>())
        {
            segment.EnableInClassList("mn-segment--selected", (Settings.DepthResolution)segment.userData == this.draft.DepthRes);
        }
        foreach (Button segment in this.root.Q("car-detail").Children().OfType<Button>())
        {
            segment.EnableInClassList("mn-segment--selected", (Settings.CarDetailLevel)segment.userData == this.draft.CarDetail);
        }
        Button[] swatches = this.root.Q("cars-swatches").Children().OfType<Button>().ToArray();
        for (int i = 0; i < swatches.Length; i++)
        {
            swatches[i].style.backgroundColor = this.draft.ShellColors[i];
            swatches[i].EnableInClassList("mn-swatch--selected", i == this.editedCar);
        }
        Color color = this.draft.ShellColors[this.editedCar];
        foreach (Button segment in this.root.Q("shell-pick").Children().OfType<Button>())
        {
            segment.EnableInClassList("mn-segment--selected", (int)segment.userData == this.shellPick);
        }
        this.root.Q("shell-sliders").style.display = this.shellPick == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        this.root.Q("shell-presets").style.display = this.shellPick == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        TextField code = this.root.Q<TextField>("shell-code");
        code.style.display = this.shellPick == 2 ? DisplayStyle.Flex : DisplayStyle.None;
        if (code.focusController?.focusedElement != code)
        {
            code.SetValueWithoutNotify(MenuController.ShellCode(color));
        }
        foreach (Button preset in this.root.Q("shell-presets").Children().OfType<Button>())
        {
            preset.EnableInClassList("mn-preset--selected", MenuController.ShellCode((Color)preset.userData) == MenuController.ShellCode(color));
        }
        this.root.Q<Slider>("shell-r").SetValueWithoutNotify(color.r);
        this.root.Q<Slider>("shell-g").SetValueWithoutNotify(color.g);
        this.root.Q<Slider>("shell-b").SetValueWithoutNotify(color.b);
        this.root.Q<TextField>("username").SetValueWithoutNotify(this.draft.Username);
        this.isLoadingDraft = false;
        this.PreviewShell();
    }

    /// <summary>
    /// Shell colors offered under Presets: the four default car colors, then common paints.
    /// </summary>
    public static readonly Color[] ShellPresets =
    {
        CarCustomization.DefaultShellColor,
        new Color(0.12f, 0.35f, 0.85f),
        new Color(0.1f, 0.6f, 0.25f),
        new Color(0.45f, 0.2f, 0.75f),
        new Color(0.85f, 0.1f, 0.1f),
        new Color(0.95f, 0.5f, 0.1f),
        new Color(0.95f, 0.8f, 0.1f),
        new Color(0.1f, 0.6f, 0.6f),
        new Color(0.95f, 0.4f, 0.65f),
        new Color(0.92f, 0.92f, 0.92f),
        new Color(0.55f, 0.57f, 0.6f),
        new Color(0.64f, 0.12f, 0.2f),
    };

    /// <summary>
    /// A color as a hex code, for example #1F59D9.
    /// </summary>
    public static string ShellCode(Color color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    /// <summary>
    /// Reads a hex color code: six hex digits, with or without a leading '#'.
    /// </summary>
    public static bool TryParseShellCode(string text, out Color color)
    {
        color = default;
        string code = (text ?? string.Empty).Trim().TrimStart('#');
        if (code.Length != 6 || !code.All(Uri.IsHexDigit))
        {
            return false;
        }
        return ColorUtility.TryParseHtmlString("#" + code, out color);
    }

    private void PreviewShell()
    {
        this.showcase.Preview(this.draft.ShellColors[this.editedCar], this.draft.IsShellShiny[this.editedCar]);
    }

    /// <summary>
    /// Fills a settings row with a label and a switch bound to a draft value.
    /// </summary>
    private static void SwitchRow(VisualElement row, string text, Func<bool> get, Action<bool> set)
    {
        Label label = new Label(text);
        label.AddToClassList("rcn-body");
        label.AddToClassList("mn-row-label");
        row.Add(label);

        Button toggle = new Button { userData = get };
        toggle.AddToClassList("mn-switch");
        VisualElement knob = new VisualElement();
        knob.AddToClassList("mn-knob");
        toggle.Add(knob);
        toggle.clicked += () =>
        {
            set(!get());
            toggle.EnableInClassList("mn-switch--on", get());
        };
        row.Add(toggle);
    }
    #endregion

    #region Best times
    private void OpenBestTimes()
    {
        ScrollView grid = this.root.Q<ScrollView>("best-times-cards");
        grid.Clear();
        Button first = null;
        foreach (LevelInfo each in LevelInfo.WinableLevels)
        {
            Button card = MenuController.LevelCard(each, () =>
            {
                MenuController.lastLevel = each;
                MenuController.lastMode = LevelManagerMode.Race;
                MenuController.lastCars = 1;
                SceneFlow.LoadLevel(LevelLaunch.Prepare(each, LevelManagerMode.Race, 1));
            });
            BestTimeInfo best = SavedDataManager.Data.GetBestTimes(each);
            if (best.OverallTime >= float.MaxValue)
            {
                Label none = new Label("--");
                none.AddToClassList("rcn-numeric");
                none.AddToClassList("mn-card-time");
                card.Q(className: "mn-card-footer").Add(none);
            }
            card.Add(MenuController.SplitBar(best));
            grid.Add(card);
            first ??= card;
        }
        this.Open(this.bestTimes, first);
    }

    /// <summary>
    /// Checkpoint splits as bar segments sized by time; empty without a full set of splits.
    /// </summary>
    private static VisualElement SplitBar(BestTimeInfo best)
    {
        VisualElement bar = new VisualElement();
        bar.AddToClassList("mn-splits");
        float[] splits = best.CheckpointTimes;
        if (best.OverallTime >= float.MaxValue || splits == null || splits.Length < 2 || splits.Any(split => split >= float.MaxValue))
        {
            return bar;
        }
        float total = splits.Sum();
        for (int i = 0; i < splits.Length; i++)
        {
            VisualElement segment = new VisualElement();
            segment.AddToClassList("mn-split");
            segment.EnableInClassList("mn-split--alternate", i % 2 == 1);
            segment.style.flexGrow = splits[i] / total;
            bar.Add(segment);
        }
        return bar;
    }

    private void ClearBestTimes()
    {
        if (this.clearArmedUntil < 0)
        {
            this.clearArmedUntil = Time.unscaledTime + MenuController.clearConfirmSeconds;
            this.clearButton.text = "Clear all?";
            return;
        }
        this.clearArmedUntil = -1;
        this.clearButton.text = "Clear";
        SavedDataManager.Data.ClearBestTimes();
        SavedDataManager.Save();
        this.OpenBestTimes();
    }
    #endregion

    #region Controls
    private void BuildControls()
    {
        this.controlsScroll = this.root.Q<ScrollView>("controls-scroll");
        this.controlsScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        this.controlsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        this.controlsScroll.RegisterCallback<GeometryChangedEvent>(_ => this.LayoutControls());
        foreach (InputBindings.Section section in InputBindings.Sections)
        {
            VisualElement column = new VisualElement();
            column.AddToClassList("mn-controls-section");
            Label title = new Label(section.Title.ToUpperInvariant());
            title.AddToClassList("rcn-eyebrow");
            title.AddToClassList("rcn-muted");
            title.AddToClassList("mn-section");
            column.Add(title);

            foreach (InputBindings.Binding binding in section.Bindings)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("mn-binding");
                Label action = new Label(binding.Action);
                action.AddToClassList("rcn-body");
                action.AddToClassList("mn-binding-action");
                row.Add(action);

                VisualElement keys = new VisualElement();
                keys.AddToClassList("mn-binding-keys");
                foreach (KeyCode[] chord in binding.Keys)
                {
                    for (int i = 0; i < chord.Length; i++)
                    {
                        if (i > 0)
                        {
                            Label join = new Label("+");
                            join.AddToClassList("rcn-key-join");
                            keys.Add(join);
                        }
                        Label cap = new Label(InputBindings.KeyLabel(chord[i]));
                        cap.AddToClassList("rcn-key");
                        keys.Add(cap);
                    }
                }
                row.Add(keys);

                Label pad = new Label(binding.Controller ?? string.Empty);
                pad.AddToClassList("mn-pad");
                row.Add(pad);
                column.Add(row);
            }
            this.controlSections.Add(column);
        }
    }

    /// <summary>
    /// Puts the Controls sections into as many columns as the width allows (1 to 3), in order,
    /// filling each column to about an equal number of rows.
    /// </summary>
    private void LayoutControls()
    {
        float width = this.controlsScroll.contentRect.width;
        int columns = Mathf.Clamp((int)(width / MenuController.controlColumnWidth), 1, 3);
        if (float.IsNaN(width) || columns == this.controlColumns)
        {
            return;
        }
        this.controlColumns = columns;

        VisualElement container = this.root.Q("controls-columns");
        container.Clear();
        int[] rows = this.controlSections.Select(section => section.childCount).ToArray();
        float perColumn = (float)rows.Sum() / columns;
        int next = 0;
        for (int c = 0; c < columns; c++)
        {
            VisualElement column = new VisualElement();
            column.AddToClassList("mn-controls-column");
            int filled = 0;
            while (next < this.controlSections.Count && (c == columns - 1 || filled == 0 || filled + rows[next] / 2f <= perColumn))
            {
                column.Add(this.controlSections[next]);
                filled += rows[next];
                next++;
            }
            container.Add(column);
        }
    }
    #endregion

    #region Dialog
    private void ShowDialog(string title, string text, string input, string ok, Action onOk, string other, Action onOther)
    {
        this.root.Q<Label>("dialog-title").text = title;
        this.root.Q<Label>("dialog-text").text = text;
        TextField field = this.root.Q<TextField>("dialog-input");
        field.style.display = input != null ? DisplayStyle.Flex : DisplayStyle.None;
        field.SetValueWithoutNotify(input ?? string.Empty);
        this.root.Q<Button>("dialog-ok").text = ok;
        Button otherButton = this.root.Q<Button>("dialog-other");
        otherButton.text = other ?? string.Empty;
        otherButton.style.display = other != null ? DisplayStyle.Flex : DisplayStyle.None;
        this.dialogOk = onOk;
        this.dialogOther = onOther;
        this.dialog.AddToClassList("mn-dialog-backdrop--open");
        (input != null ? (Focusable)field : this.root.Q<Button>("dialog-ok")).Focus();
    }

    private void CloseDialog(Action then)
    {
        this.dialog.RemoveFromClassList("mn-dialog-backdrop--open");
        this.dialogOk = null;
        this.dialogOther = null;
        then?.Invoke();
        if (then == null)
        {
            this.playButton.Focus();
        }
    }
    #endregion
}
