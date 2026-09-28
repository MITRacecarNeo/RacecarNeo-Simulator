using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles the main menu.
/// </summary>
public class MainMenu : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The game object containing the elements which allow the user to select the number of cars to spawn in the level.
    /// </summary>
    [SerializeField]
    private GameObject numCars;

    /// <summary>
    /// The level collection dropdown.
    /// </summary>
    [SerializeField]
    private Dropdown collectionDropdown;

    /// <summary>
    /// The level dropdown.
    /// </summary>
    [SerializeField]
    private Dropdown levelDropdown;

    /// <summary>
    /// The mode dropdown, listing LevelInfo.SupportedModes of the selected level.
    /// </summary>
    [SerializeField]
    private Dropdown modeDropdown;

    /// <summary>
    /// The number of cars dropdown.
    /// </summary>
    [SerializeField]
    private Dropdown numCarsDropdown;
    #endregion

    #region Constants
    /// <summary>
    /// The keys of the Konami Code.
    /// </summary>
    public static KeyCode[] KonamiCodes =
    {
        KeyCode.UpArrow,
        KeyCode.UpArrow,
        KeyCode.DownArrow,
        KeyCode.DownArrow,
        KeyCode.LeftArrow,
        KeyCode.RightArrow,
        KeyCode.LeftArrow,
        KeyCode.RightArrow,
        KeyCode.B,
        KeyCode.A
    };
    #endregion

    #region Public Interface
    /// <summary>
    /// Loads the level selected in the dropdown menu.
    /// </summary>
    public void BeginSimulation()
    {
        // Cache the current level and collection indices so we remember them the next time we load the main menu
        MainMenu.prevCollectionIndex = this.collectionDropdown.value;
        MainMenu.prevLevelIndex = this.levelDropdown.value;

        LevelManager.NumPlayers = this.numCarsDropdown.value + 1;
        LevelManager.LevelManagerMode = this.SelectedLevel.SupportedModes[this.modeDropdown.value];

        LevelManager.LevelInfo = this.SelectedLevel;
        int buildIndex = -1; // set the build index to a default null value

        // If random maps is selected as a level option (HasRandomMaps), then set the build index to one of the maps specified in RandomSceneBuildIndices
        // If random maps is not selected, OR if the current level manager mode is Exploration, use the default build index option
        int[] candidates = this.SelectedLevel.RandomSceneBuildIndices;
        if (!this.SelectedLevel.HasRandomMaps || LevelManager.LevelManagerMode == LevelManagerMode.Exploration || candidates == null || candidates.Length == 0)
        {
            buildIndex = LevelManager.LevelManagerMode == LevelManagerMode.Autograder ? this.SelectedLevel.AutograderBuildIndex : this.SelectedLevel.BuildIndex;
        }
        else
        {
            int rand = UnityEngine.Random.Range(0, candidates.Length);
            buildIndex = candidates[rand]; // choose a random level to run for autograder or race mode
        }

        // If this is the first time the user has selected an autograder without setting their username, show dialog reminding them to set their username
        if (LevelManager.LevelManagerMode == LevelManagerMode.Autograder &&
            !SavedDataManager.Data.WasUsernameDialogShown &&
            Settings.Username == Settings.DefaultUsername)
        {
            this.usernamePane.Initialize(buildIndex);
            this.usernamePane.gameObject.SetActive(true);            
            SavedDataManager.Data.WasUsernameDialogShown = true;
            SavedDataManager.Save();
        }
        else
        {
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Shows the controls screen.
    /// </summary>
    public void ShowControls()
    {
        this.controlsPane.gameObject.SetActive(true);
    }

    /// <summary>
    /// Shows the settings screen.
    /// </summary>
    public void ShowSettings()
    {
        this.settingsPane.gameObject.SetActive(true);
    }

    /// <summary>
    /// Show the best times screen.
    /// </summary>
    public void ShowBestTimes()
    {
        this.bestTimesPane.UpdateEntries();
        this.bestTimesPane.gameObject.SetActive(true);
    }

    /// <summary>
    /// Handles when the user selects a new value in the level collection dropdown.
    /// </summary>
    /// <remarks>This overload exists to be called from Unity, since Unity cannot call functions with multiple parameters.</remarks>
    public void HandleLevelCollectionDropdownChange()
    {
        this.HandleLevelCollectionDropdownChange(0, LevelManagerMode.Exploration, 1);
    }

    /// <summary>
    /// Handles when the user selects a new value in the level collection dropdown.
    /// </summary>
    /// <param name="selectedLevel">The index of the level which should be selected by default in the level select menu.</param>
    /// <param name="mode">The LevelManagerMode which should be selected.</param>
    /// <param name="numCars">The number of cars which should be selected.</param>
    public void HandleLevelCollectionDropdownChange(int selectedLevel, LevelManagerMode mode, int numCars)
    {
        this.levelDropdown.ClearOptions();
        this.levelDropdown.AddOptions(this.SelectedLevelCollection.LevelNames);
        this.levelDropdown.value = selectedLevel;

        this.HandleLevelDropdownChange(mode, numCars);
    }

    /// <summary>
    /// Handles when the user selects a new value in the level dropdown.
    /// </summary>
    /// <remarks>This overload exists to be called from Unity, since Unity cannot call functions with multiple parameters.</remarks>
    public void HandleLevelDropdownChange()
    {
        this.HandleLevelDropdownChange(LevelManagerMode.Exploration, 1);
    }

    /// <summary>
    /// Handles when the user selects a new value in the level dropdown.
    /// </summary>
    /// <param name="mode">The LevelManagerMode which should be selected.</param>
    /// <param name="numCars">The number of cars which should be selected.</param>
    public void HandleLevelDropdownChange(LevelManagerMode mode, int numCars)
    {
        // Offer only the modes the level supports; a mode it lacks falls back to exploration
        LevelManagerMode[] modes = this.SelectedLevel.SupportedModes;
        this.modeDropdown.options = modes.Select(supported => new Dropdown.OptionData(supported.ToString())).ToList();
        this.modeDropdown.interactable = modes.Length > 1;
        this.modeDropdown.value = Mathf.Max(Array.IndexOf(modes, mode), 0);
        this.modeDropdown.RefreshShownValue();

        // Show and populate the numCars dropdown if the level supports multiple cars
        if (this.SelectedLevel.MaxCars > 1)
        {
            if (this.SelectedLevel.MaxCars != this.numCarsDropdown.options.Count)
            {
                List<string> options = new List<string>(this.SelectedLevel.MaxCars);
                for (int i = 1; i <= this.SelectedLevel.MaxCars; i++)
                {
                    options.Add(i.ToString());
                }
                this.numCarsDropdown.ClearOptions();
                this.numCarsDropdown.AddOptions(options);
            }
            this.numCars.SetActive(true);
        }
        else
        {
            this.numCars.SetActive(false);
        }

        // Regardless of whether it is shown, we always set the dropdown value since it determines NumPlayers when the level is loaded
        this.numCarsDropdown.value = numCars - 1;
    }

    /// <summary>
    /// Handles when the user selects a new value in the num cars dropdown.
    /// </summary>
    public void HandleNumCarsChange()
    {
        // Lock the mode dropdown to "Race" if the user chcose multiple cars
        if (this.numCarsDropdown.value > 0)
        {
            this.modeDropdown.value = Array.IndexOf(this.SelectedLevel.SupportedModes, LevelManagerMode.Race);
            this.modeDropdown.interactable = false;
        }
        else
        {
            this.modeDropdown.interactable = true;
        }
    }

    /// <summary>
    /// Close the program.
    /// </summary>
    public void Exit()
    {
        Application.Quit(0);
    }

    /// <summary>
    /// The current level collection selected in the dropdown menu.
    /// </summary>
    public LevelCollection SelectedLevelCollection
    {
        get
        {
            return LevelCollection.LevelCollections[this.collectionDropdown.value];
        }
    }

    /// <summary>
    /// The current level selected in the dropdown menu.
    /// </summary>
    public LevelInfo SelectedLevel
    {
        get
        {
            return SelectedLevelCollection.Levels[this.levelDropdown.value];
        }
    }
    #endregion

    /// <summary>
    /// The index of the level collection selected the last time we loaded the main menu.
    /// </summary>
    private static int prevCollectionIndex = 0;

    /// <summary>
    /// The index of the level selected the last time we loaded the main menu.
    /// </summary>
    private static int prevLevelIndex = 0;

    /// <summary>
    /// The screen which shows the controls.
    /// </summary>
    private ControlsUI controlsPane;

    /// <summary>
    /// The screen which allows the user to adjust settings.
    /// </summary>
    private SettingsUI settingsPane;

    /// <summary>
    /// The screen which shows the user's best times.
    /// </summary>
    private BestTimesUI bestTimesPane;

    /// <summary>
    /// The screen which prompts the user to set their username.
    /// </summary>
    private NoUsernameUI usernamePane;

    /// <summary>
    /// The index of the next key in the Konami Code which the user must press.
    /// </summary>
    private int konamiCodeIndex = 0;

    /// <summary>
    /// Returns how many keys of the Konami Code are matched after pressing a key, reusing any
    /// matched suffix so a wrong key that starts the code again is not lost.
    /// </summary>
    /// <param name="matched">The number of code keys matched before this key.</param>
    /// <param name="key">The key pressed.</param>
    /// <returns>The longest code prefix that ends the typed sequence.</returns>
    public static int NextKonamiIndex(int matched, KeyCode key)
    {
        KeyCode[] code = MainMenu.KonamiCodes;
        for (int length = Mathf.Min(matched + 1, code.Length); length > 0; length--)
        {
            if (code[length - 1] != key)
            {
                continue;
            }

            bool isMatch = true;
            for (int i = 0; isMatch && i < length - 1; i++)
            {
                isMatch = code[i] == code[matched - length + 1 + i];
            }
            if (isMatch)
            {
                return length;
            }
        }
        return 0;
    }

    private void Awake()
    {

        this.controlsPane = this.GetComponentInChildren<ControlsUI>();
        this.settingsPane = this.GetComponentInChildren<SettingsUI>();
        this.bestTimesPane = this.GetComponentInChildren<BestTimesUI>();
        this.usernamePane = this.GetComponentInChildren<NoUsernameUI>();

        if (SavedDataManager.WasLegacyDataReset)
        {
            SavedDataManager.WasLegacyDataReset = false;
            this.gameObject.AddComponent<SaveResetNotice>();
        }
    }

    private void Start()
    {
        // Hide panes
        this.controlsPane.gameObject.SetActive(false);
        this.settingsPane.gameObject.SetActive(false);
        this.bestTimesPane.gameObject.SetActive(false);
        this.usernamePane.gameObject.SetActive(false);

        this.numCars.SetActive(false);

        // Populate level collection dropdown
        this.collectionDropdown.ClearOptions();
        List<string> collectionDisplayNames = new List<string>(LevelCollection.LevelCollections.Length);
        foreach (LevelCollection levelCollection in LevelCollection.LevelCollections)
        {
            collectionDisplayNames.Add(levelCollection.DisplayName);
        }
        this.collectionDropdown.AddOptions(collectionDisplayNames);
        this.collectionDropdown.value = MainMenu.prevCollectionIndex;

        // Begin with the previous level selection
        this.HandleLevelCollectionDropdownChange(MainMenu.prevLevelIndex, LevelManager.LevelManagerMode, LevelManager.NumPlayers);

        // The version label follows PlayerSettings.bundleVersion, the single version source
        Text versionText = this.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name == "Version");
        if (versionText != null)
        {
            versionText.text = $"Release v{Application.version}";
        }

        // A level requested on the command line (LaunchOptions) skips the menu once
        if (LaunchOptions.TryTakeLevelRequest(out LevelInfo requested))
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
            int buildIndex = LevelManager.LevelManagerMode == LevelManagerMode.Autograder ? requested.AutograderBuildIndex : requested.BuildIndex;
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
        }
    }

    private void Update()
    {
        if (!Input.anyKeyDown)
        {
            return;
        }

        KeyCode? pressed = MainMenu.KonamiCodes.Distinct().Where(Input.GetKeyDown).Select(key => (KeyCode?)key).FirstOrDefault();
        this.konamiCodeIndex = pressed.HasValue ? MainMenu.NextKonamiIndex(this.konamiCodeIndex, pressed.Value) : 0;
        if (this.konamiCodeIndex == MainMenu.KonamiCodes.Length)
        {
            Debug.Log("Cheat mode activated");
            Settings.CheatMode = true;
            this.konamiCodeIndex = 0;
        }
    }
}
