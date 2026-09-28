using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

/// <summary>
/// Contains general helpful functions.
/// </summary>
public static partial class Utilities
{
    /// <summary>
    /// Optional autograder key file, relative to the project root: 32 hex characters.
    /// Read by the editor and embedded into player builds by AutograderKeyBuildStep.
    /// </summary>
    public const string KeyFilePath = "secrets/autograder-key.txt";

    /// <summary>
    /// True when an autograder key is available and score codes can be generated.
    /// </summary>
    public static bool HasKey
    {
        get { return Utilities.key != null; }
    }

    /// <summary>
    /// Prefix of a version 2 score code. Version 1 codes are bare hex and never contain 'R'.
    /// </summary>
    public const string ScoreCodePrefix = "R2";

    /// <summary>
    /// Encrypts a score-code payload with the autograder key (score code version 2).
    /// </summary>
    /// <param name="message">The plaintext payload.</param>
    /// <returns>The score code: ScoreCodePrefix followed by uppercase hex.</returns>
    public static string Encrypt(string message)
    {
        return Utilities.Encrypt(message, Utilities.key);
    }

    /// <summary>
    /// Encrypts a score-code payload with an explicit 16-byte key and a random IV.
    /// </summary>
    /// <param name="message">The plaintext payload.</param>
    /// <param name="key">The 16-byte autograder key; null when no key is available.</param>
    /// <returns>The score code.</returns>
    public static string Encrypt(string message, byte[] key)
    {
        byte[] iv = new byte[16];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(iv);
        }
        return Utilities.Encrypt(message, key, iv);
    }

    /// <summary>
    /// Encrypts a score-code payload with an explicit key and IV. Only tests pass a fixed IV.
    /// </summary>
    /// <remarks>
    /// Format (version 2): "R2" + hex(iv[16] || ciphertext || tag[16]).
    /// encKey = HMAC-SHA256(key, "racecarsim-score-code-v2-enc")[0..16];
    /// macKey = HMAC-SHA256(key, "racecarsim-score-code-v2-mac");
    /// ciphertext = AES-128-CBC-PKCS7(encKey, iv, UTF-8(message));
    /// tag = HMAC-SHA256(macKey, "R2" || iv || ciphertext)[0..16].
    /// The key ships in every build, so codes resist casual editing but not a determined forger.
    /// </remarks>
    /// <param name="message">The plaintext payload.</param>
    /// <param name="key">The 16-byte autograder key.</param>
    /// <param name="iv">The 16-byte initialization vector.</param>
    /// <returns>The score code.</returns>
    public static string Encrypt(string message, byte[] key, byte[] iv)
    {
        if (key == null)
        {
            throw new InvalidOperationException("Autograder key missing.");
        }

        (byte[] encKey, byte[] macKey) = Utilities.DeriveScoreCodeKeys(key);
        byte[] ciphertext;
        using (Aes aes = Aes.Create())
        {
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using (ICryptoTransform encryptor = aes.CreateEncryptor(encKey, iv))
            {
                byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(message);
                ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }
        }

        byte[] tag = Utilities.ScoreCodeTag(macKey, iv, ciphertext);
        byte[] body = iv.Concat(ciphertext).Concat(tag).ToArray();
        return Utilities.ScoreCodePrefix + BitConverter.ToString(body).Replace("-", "");
    }

    /// <summary>
    /// Decrypts a version 2 score code, as the grading software does.
    /// </summary>
    /// <param name="code">The score code.</param>
    /// <param name="key">The 16-byte autograder key.</param>
    /// <returns>The payload, or null if the code is malformed or fails authentication.</returns>
    public static string DecryptScoreCode(string code, byte[] key)
    {
        if (code == null || !code.StartsWith(Utilities.ScoreCodePrefix, StringComparison.Ordinal))
        {
            return null;
        }
        string hex = code.Substring(Utilities.ScoreCodePrefix.Length);
        if (hex.Length % 2 != 0 || !hex.All(Uri.IsHexDigit) || hex.Length < 2 * (16 + 16 + 16))
        {
            return null;
        }

        byte[] body = Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(2 * i, 2), 16)).ToArray();
        byte[] iv = body.Take(16).ToArray();
        byte[] ciphertext = body.Skip(16).Take(body.Length - 32).ToArray();
        byte[] tag = body.Skip(body.Length - 16).ToArray();
        if (ciphertext.Length % 16 != 0)
        {
            return null;
        }

        (byte[] encKey, byte[] macKey) = Utilities.DeriveScoreCodeKeys(key);
        byte[] expected = Utilities.ScoreCodeTag(macKey, iv, ciphertext);
        int difference = 0;
        for (int i = 0; i < 16; i++)
        {
            difference |= expected[i] ^ tag[i];
        }
        if (difference != 0)
        {
            return null;
        }

        using (Aes aes = Aes.Create())
        {
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using (ICryptoTransform decryptor = aes.CreateDecryptor(encKey, iv))
            {
                byte[] plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
                return System.Text.Encoding.UTF8.GetString(plaintext);
            }
        }
    }

    /// <summary>
    /// Derives the score-code encryption key (16 bytes) and MAC key (32 bytes) from the autograder key.
    /// </summary>
    private static (byte[] encKey, byte[] macKey) DeriveScoreCodeKeys(byte[] key)
    {
        using (HMACSHA256 hmac = new HMACSHA256(key))
        {
            byte[] encKey = hmac.ComputeHash(System.Text.Encoding.ASCII.GetBytes("racecarsim-score-code-v2-enc")).Take(16).ToArray();
            byte[] macKey = hmac.ComputeHash(System.Text.Encoding.ASCII.GetBytes("racecarsim-score-code-v2-mac"));
            return (encKey, macKey);
        }
    }

    /// <summary>
    /// Computes the 16-byte truncated HMAC-SHA256 tag over the prefix, IV, and ciphertext.
    /// </summary>
    private static byte[] ScoreCodeTag(byte[] macKey, byte[] iv, byte[] ciphertext)
    {
        byte[] data = System.Text.Encoding.ASCII.GetBytes(Utilities.ScoreCodePrefix).Concat(iv).Concat(ciphertext).ToArray();
        using (HMACSHA256 hmac = new HMACSHA256(macKey))
        {
            return hmac.ComputeHash(data).Take(16).ToArray();
        }
    }
    
    /// <summary>
    /// Parses a 16-byte key written as 32 hex characters; whitespace is ignored.
    /// </summary>
    /// <param name="text">The key text.</param>
    /// <returns>The key bytes, or null when the text is not exactly 16 hex bytes.</returns>
    public static byte[] ParseKeyHex(string text)
    {
        string hex = string.Concat((text ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));
        if (hex.Length != 32 || !hex.All(Uri.IsHexDigit))
        {
            return null;
        }

        byte[] bytes = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(2 * i, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// The autograder key, or null when no key is available.
    /// </summary>
    private static readonly byte[] key = Utilities.LoadKey();

    /// <summary>
    /// Supplies the key in player builds. Implemented by AutograderKey.Generated.cs, which
    /// AutograderKeyBuildStep writes for the duration of a build.
    /// </summary>
    static partial void SetKey(ref byte[] key);

    private static byte[] LoadKey()
    {
        byte[] loaded = null;
        Utilities.SetKey(ref loaded);
#if UNITY_EDITOR
        if (loaded == null && File.Exists(Utilities.KeyFilePath))
        {
            loaded = Utilities.ParseKeyHex(File.ReadAllText(Utilities.KeyFilePath));
        }
#endif
        return loaded;
    }
}
