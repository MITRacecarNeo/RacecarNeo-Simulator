using UnityEngine;

/// <summary>
/// One-time overlay telling the player that best times saved by an earlier version were reset.
/// Added by MainMenu when SavedDataManager.WasLegacyDataReset is set; removes itself on OK.
/// </summary>
public class SaveResetNotice : MonoBehaviour
{
    /// <summary>
    /// The notice text.
    /// </summary>
    public const string Message =
        "This version stores best times in a new format. Best times and car colors saved by earlier versions have been reset.";

    private void Start()
    {
        Debug.Log("Save reset notice shown.");
    }

    private void OnGUI()
    {
        float unit = Screen.height / 40.0f;
        float width = Mathf.Min(Screen.width - 2 * unit, 30 * unit);
        float height = 9 * unit;
        Rect box = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);

        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(unit),
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(unit) };

        // Drawn twice because the default box skin is translucent over the menu
        GUI.Box(box, GUIContent.none);
        GUI.Box(box, GUIContent.none);
        GUI.Label(new Rect(box.x + unit, box.y + unit, box.width - 2 * unit, 5 * unit), SaveResetNotice.Message, label);
        if (GUI.Button(new Rect(box.center.x - 3 * unit, box.yMax - 2.5f * unit, 6 * unit, 1.8f * unit), "OK", button))
        {
            Object.Destroy(this);
        }
    }
}
