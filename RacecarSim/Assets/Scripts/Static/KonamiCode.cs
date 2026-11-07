using UnityEngine;

/// <summary>
/// The Konami Code on the main menu, which turns on Settings.CheatMode.
/// </summary>
public static class KonamiCode
{
    /// <summary>
    /// The keys of the code, in order.
    /// </summary>
    public static readonly KeyCode[] Keys =
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

    /// <summary>
    /// Returns how many keys of the code are matched after pressing a key, reusing any matched
    /// suffix so a wrong key that starts the code again is not lost.
    /// </summary>
    /// <param name="matched">The number of code keys matched before this key.</param>
    /// <param name="key">The key pressed.</param>
    /// <returns>The longest code prefix that ends the typed sequence.</returns>
    public static int NextIndex(int matched, KeyCode key)
    {
        KeyCode[] code = KonamiCode.Keys;
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
}
