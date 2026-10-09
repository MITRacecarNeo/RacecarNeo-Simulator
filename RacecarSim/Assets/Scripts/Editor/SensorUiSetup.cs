using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the UI for the sensor and actuator features to the HUD prefab and the settings pane, and
/// renders previews of both. Each step skips what already exists, so Apply can run again.
/// </summary>
/// <remarks>
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod SensorUiSetup.Apply</code>
/// <code>Unity -batchmode -quit -projectPath . -executeMethod SensorUiSetup.RenderPreviews</code>
/// Previews go to Logs/ui-preview-hud.png and Logs/ui-preview-settings.png.
/// </remarks>
public static class SensorUiSetup
{
    private const string hudPrefabPath = "Assets/Prefabs/UI/Hud.prefab";
    private const string mainScenePath = "Assets/Scenes/Main.unity";

    /// <summary>
    /// Adds every missing UI element and wires its serialized reference.
    /// </summary>
    public static void Apply()
    {
        SensorUiSetup.AddHudBattery();
        SensorUiSetup.AddHudDotMatrix();
        SensorUiSetup.AddSettingsToggles();
        SensorUiSetup.UseShellColors();
    }

    /// <summary>
    /// Renders the HUD and the settings pane to PNG files in Logs/.
    /// </summary>
    public static void RenderPreviews()
    {
        // The dot matrix draws at run time; the preview shows the MAN glyph
        GameObject hud = PrefabUtility.LoadPrefabContents(SensorUiSetup.hudPrefabPath);
        Texture2D matrix = DotMatrixPanel.CreateTexture();
        bool[,] frame = new DotMatrixDisplay(0).Frame(new ActuatorCommands(), DotMatrixDisplay.DriveMode.Manual, DotMatrixDisplay.SplashPeriod);
        DotMatrixPanel.DrawFrame(matrix, new Color32[matrix.width * matrix.height], frame);
        hud.transform.Find("DotMatrix/Display").GetComponent<RawImage>().texture = matrix;
        SensorUiSetup.RenderCanvas(hud.GetComponentInChildren<Canvas>(true), "Logs/ui-preview-hud.png");
        PrefabUtility.UnloadPrefabContents(hud);
        Object.DestroyImmediate(matrix);

        // Show the settings pane alone: the other main menu panes share its canvas
        EditorSceneManager.OpenScene(SensorUiSetup.mainScenePath, OpenSceneMode.Single);
        SettingsUI settings = SensorUiSetup.FindInScene<SettingsUI>();
        foreach (Transform sibling in settings.transform.parent)
        {
            sibling.gameObject.SetActive(sibling == settings.transform);
        }
        SensorUiSetup.RenderCanvas(settings.GetComponentInParent<Canvas>(true).rootCanvas, "Logs/ui-preview-settings.png");
    }

    /// <summary>
    /// HUD: a Battery readout (voltage and current) built from the TrueSpeed readout, on the
    /// right edge below the IMU readout.
    /// </summary>
    private static void AddHudBattery()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SensorUiSetup.hudPrefabPath);
        try
        {
            if (root.transform.Find("Battery") != null)
            {
                return;
            }

            Transform trueSpeed = root.transform.Find("TrueSpeed");
            GameObject battery = Object.Instantiate(trueSpeed.gameObject, trueSpeed.parent);
            battery.name = "Battery";
            RectTransform rect = (RectTransform)battery.transform;
            rect.anchorMin = new Vector2(0.8f, 0.66f);
            rect.anchorMax = new Vector2(0.99f, 0.8f);

            battery.transform.Find("Title").GetComponent<Text>().text = "Battery";
            Object.DestroyImmediate(battery.transform.Find("Units").gameObject);
            Text value = battery.transform.Find("Value").GetComponent<Text>();
            RectTransform valueRect = (RectTransform)value.transform;
            valueRect.anchorMin = new Vector2(0, 0);
            valueRect.anchorMax = new Vector2(1, 0.55f);
            value.text = "8.40 V   2.50 A";
            value.alignment = TextAnchor.MiddleRight;

            SerializedObject hud = new SerializedObject(root.GetComponent<Hud>());
            hud.FindProperty("batteryText").objectReferenceValue = value;
            hud.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SensorUiSetup.hudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// HUD: the dot matrix panel on the right edge between the slow-motion label and the Battery
    /// readout; the image keeps the matrix's 3:1 shape so the LEDs stay round.
    /// </summary>
    private static void AddHudDotMatrix()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SensorUiSetup.hudPrefabPath);
        try
        {
            if (root.transform.Find("DotMatrix") != null)
            {
                return;
            }

            GameObject panel = new GameObject("DotMatrix", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.78f, 0.55f);
            rect.anchorMax = new Vector2(0.99f, 0.655f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            GameObject display = new GameObject("Display", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            display.transform.SetParent(panel.transform, false);
            RawImage image = display.GetComponent<RawImage>();
            image.raycastTarget = false;
            AspectRatioFitter fitter = display.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)ActuatorCommands.MatrixColumns / ActuatorCommands.MatrixRows;

            DotMatrixPanel matrix = panel.AddComponent<DotMatrixPanel>();
            SerializedObject serializedMatrix = new SerializedObject(matrix);
            serializedMatrix.FindProperty("image").objectReferenceValue = image;
            serializedMatrix.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject hud = new SerializedObject(root.GetComponent<Hud>());
            hud.FindProperty("dotMatrixPanel").objectReferenceValue = matrix;
            hud.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SensorUiSetup.hudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Settings pane: the Realism row splits into Realism and Battery mode; the Hide Cars row into
    /// Hide cars and Show dot matrix.
    /// </summary>
    private static void AddSettingsToggles()
    {
        EditorSceneManager.OpenScene(SensorUiSetup.mainScenePath, OpenSceneMode.Single);
        SettingsUI settings = SensorUiSetup.FindInScene<SettingsUI>();
        SerializedObject serialized = new SerializedObject(settings);
        bool changed = false;

        if (serialized.FindProperty("batteryModeToggle").objectReferenceValue == null)
        {
            Toggle realism = (Toggle)serialized.FindProperty("realismToggle").objectReferenceValue;
            Toggle battery = SensorUiSetup.SplitToggleRow(realism, "BatteryMode", "<b>Battery mode</b>: Stops the car when battery runs out.");
            realism.transform.Find("Label").GetComponent<Text>().text = "<b>Realism</b>: realistic error on all sensors.";
            serialized.FindProperty("batteryModeToggle").objectReferenceValue = battery;
            changed = true;
        }

        if (serialized.FindProperty("showDotMatrixToggle").objectReferenceValue == null)
        {
            Toggle hideCars = (Toggle)serialized.FindProperty("hideCarsToggle").objectReferenceValue;
            Toggle matrix = SensorUiSetup.SplitToggleRow(hideCars, "ShowDotMatrix", "<b>Show dot matrix</b>: 8x24 display on screen");
            hideCars.transform.Find("Label").GetComponent<Text>().text = "<b>Hide cars</b>: Remove other cars from color camera";
            serialized.FindProperty("showDotMatrixToggle").objectReferenceValue = matrix;
            changed = true;
        }

        if (changed)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            EditorSceneManager.SaveScene(settings.gameObject.scene);
        }
    }

    /// <summary>
    /// Settings pane: renames each car's Front sliders and Shiny toggle to Shell and deletes the
    /// Back set.
    /// </summary>
    private static void UseShellColors()
    {
        EditorSceneManager.OpenScene(SensorUiSetup.mainScenePath, OpenSceneMode.Single);
        SettingsUI settings = SensorUiSetup.FindInScene<SettingsUI>();
        SerializedObject serialized = new SerializedObject(settings);
        SerializedProperty sliders = serialized.FindProperty("colorSliders");
        SerializedProperty shiny = serialized.FindProperty("shinyToggles");
        Transform pane = settings.transform;
        if (pane.Find("Car1Colors/ShellRed") != null)
        {
            return;
        }

        int cars = 0;
        while (pane.Find($"Car{cars + 1}Colors") != null)
        {
            cars++;
        }
        sliders.arraySize = 3 * cars;
        shiny.arraySize = cars;
        for (int car = 0; car < cars; car++)
        {
            Transform block = pane.Find($"Car{car + 1}Colors");
            foreach (string part in new[] { "BackLabel", "BackRed", "BackGreen", "BackBlue", "BackShiny" })
            {
                Object.DestroyImmediate(block.Find(part).gameObject);
            }
            foreach (string part in new[] { "Label", "Red", "Green", "Blue", "Shiny" })
            {
                block.Find("Front" + part).name = "Shell" + part;
            }
            block.Find("ShellLabel").GetComponent<Text>().text = $"Car {car + 1} Shell";
            string[] channels = { "Red", "Green", "Blue" };
            for (int c = 0; c < 3; c++)
            {
                sliders.GetArrayElementAtIndex(3 * car + c).objectReferenceValue = block.Find("Shell" + channels[c]).GetComponent<Slider>();
            }
            shiny.GetArrayElementAtIndex(car).objectReferenceValue = block.Find("ShellShiny").GetComponent<Toggle>();
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
        EditorSceneManager.SaveScene(settings.gameObject.scene);
    }

    /// <summary>
    /// Narrows a full-width toggle row to its left half and adds a copy in the right half.
    /// </summary>
    /// <returns>The new toggle.</returns>
    private static Toggle SplitToggleRow(Toggle left, string name, string label)
    {
        RectTransform leftRect = (RectTransform)left.transform;
        float x0 = leftRect.anchorMin.x;
        float x1 = leftRect.anchorMax.x;
        float middle = (x0 + x1) / 2;

        GameObject copy = Object.Instantiate(left.gameObject, left.transform.parent);
        copy.name = name;
        copy.transform.SetSiblingIndex(left.transform.GetSiblingIndex() + 1);
        copy.transform.Find("Label").GetComponent<Text>().text = label;
        Toggle right = copy.GetComponent<Toggle>();
        right.onValueChanged.RemoveAllListeners();

        leftRect.anchorMax = new Vector2(middle - 0.02f, leftRect.anchorMax.y);
        RectTransform rightRect = (RectTransform)copy.transform;
        rightRect.anchorMin = new Vector2(middle + 0.02f, leftRect.anchorMin.y);
        rightRect.anchorMax = new Vector2(x1, leftRect.anchorMax.y);

        // The checkbox and label are sized as fractions of the row; keep their on-screen size
        float scale = (x1 - x0) / (leftRect.anchorMax.x - x0);
        foreach (RectTransform row in new[] { leftRect, rightRect })
        {
            RectTransform box = (RectTransform)row.Find("Background");
            box.anchorMax = new Vector2(box.anchorMax.x * scale, box.anchorMax.y);
            RectTransform text = (RectTransform)row.Find("Label");
            text.anchorMin = new Vector2(text.anchorMin.x * scale, text.anchorMin.y);
        }
        return right;
    }

    private static T FindInScene<T>() where T : Component
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
            {
                return found;
            }
        }
        throw new System.InvalidOperationException($"No {typeof(T).Name} in {SensorUiSetup.mainScenePath}");
    }

    /// <summary>
    /// Renders a screen-space canvas at 1920x1080 through a temporary camera.
    /// </summary>
    private static void RenderCanvas(Canvas canvas, string path)
    {
        RenderMode mode = canvas.renderMode;
        Camera previous = canvas.worldCamera;
        GameObject cameraObject = new GameObject("PreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();

        // Prefab contents live in their own preview scene, which a camera renders only when told to
        camera.scene = canvas.gameObject.scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.35f, 0.4f, 0.45f);
        RenderTexture texture = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = texture;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture.active = texture;
            Texture2D image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            RenderTexture.active = null;
            canvas.renderMode = mode;
            canvas.worldCamera = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
