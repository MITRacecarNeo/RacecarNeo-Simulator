using System;
using UnityEngine;

/// <summary>
/// The scenery palette an arena uses: the sky, ground, and mountain colors change; structures
/// keep the brand neutrals.
/// </summary>
public enum ArenaPalette
{
    Day,
    Sunset,
    Autumn,
}

/// <summary>
/// Scenery around the shared terrain (nested in Terrain.prefab): gradient sky dome, outer ground,
/// mountain ring, grandstands, light towers, and a kerb barrier at the foot of the cliffs. Every
/// part is on the Arena layer, which the car's cameras and the HUD map do not render, and has no
/// collider, so camera, depth, and LIDAR readings are the same as without it. Colors come from the
/// level's palette (LevelPresentation.ArenaPalette) through material property blocks.
/// </summary>
[ExecuteAlways]
public class Arena : MonoBehaviour
{
    /// <summary>
    /// What a renderer shows, which sets its color.
    /// </summary>
    public enum Role
    {
        Sky,
        Ground,
        MountainNear,
        MountainMid,
        MountainFar,
        Structure,
        StructureDark,
        Seat,
        Fascia,
        KerbRed,
        KerbWhite,
        Lamp,
    }

    /// <summary>
    /// A renderer and its role.
    /// </summary>
    [Serializable]
    public struct Part
    {
        public Renderer Renderer;
        public Role Role;
    }

    #region Set in Unity Editor
    /// <summary>
    /// Every renderer in the arena with its role (written by ArenaSetup).
    /// </summary>
    [SerializeField]
    private Part[] parts = new Part[0];

    /// <summary>
    /// The palette shown in the editor; in play mode the level's palette applies.
    /// </summary>
    [SerializeField]
    private ArenaPalette editorPalette = ArenaPalette.Day;
    #endregion

    #region Public Interface
    /// <summary>
    /// The layer every arena part is on.
    /// </summary>
    public const string LayerName = "Arena";

    /// <summary>
    /// The palette currently applied.
    /// </summary>
    public ArenaPalette Palette { get; private set; }

    /// <summary>
    /// Culling mask bit of the Arena layer, for cameras that must not render the arena.
    /// </summary>
    public static int LayerMaskBit => 1 << LayerMask.NameToLayer(Arena.LayerName);

    /// <summary>
    /// Scenery token paths for a palette: sky top, sky horizon, ground, near, mid, and far mountains.
    /// </summary>
    public static (string Sky, string Horizon, string Ground, string Near, string Mid, string Far) SceneryTokens(ArenaPalette palette)
    {
        switch (palette)
        {
            case ArenaPalette.Sunset:
                return ("illustration.sunset.sky", "illustration.sunset.cloud", "illustration.sunset.ridge-dark",
                    "illustration.sunset.ridge-dark", "illustration.sunset.ridge", "illustration.sunset.mountain-mid");
            case ArenaPalette.Autumn:
                return ("illustration.autumn.sky", "illustration.autumn.water", "illustration.autumn.ground-shadow",
                    "illustration.autumn.ground", "illustration.autumn.leaf-red", "illustration.autumn.leaf-orange");
            default:
                return ("illustration.day.sky", "illustration.day.cloud", "illustration.day.grass-mid",
                    "illustration.day.mountain-dark", "illustration.day.mountain-mid", "illustration.day.mountain-light");
        }
    }

    /// <summary>
    /// The color for a role in a palette.
    /// </summary>
    public static Color ColorFor(Role role, ArenaPalette palette)
    {
        var tokens = Arena.SceneryTokens(palette);
        switch (role)
        {
            case Role.Ground: return ThemeTokens.Scenery[tokens.Ground];
            case Role.MountainNear: return ThemeTokens.Scenery[tokens.Near];
            case Role.MountainMid: return ThemeTokens.Scenery[tokens.Mid];
            case Role.MountainFar: return ThemeTokens.Scenery[tokens.Far];
            case Role.Structure: return ThemeTokens.Slate;
            case Role.StructureDark: return ThemeTokens.Charcoal;
            case Role.Seat: return ThemeTokens.Steel;
            case Role.Fascia: return ThemeTokens.Crimson;
            case Role.KerbRed: return ThemeTokens.Red;
            case Role.KerbWhite: return ThemeTokens.Paper;
            case Role.Lamp: return ThemeTokens.Paper;
            default: return ThemeTokens.Scenery[tokens.Sky];
        }
    }

    /// <summary>
    /// Colors every part for a palette.
    /// </summary>
    public void Apply(ArenaPalette palette)
    {
        this.Palette = palette;
        var tokens = Arena.SceneryTokens(palette);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Part part in this.parts)
        {
            if (part.Renderer == null)
            {
                continue;
            }
            block.Clear();
            if (part.Role == Role.Sky)
            {
                block.SetColor(Arena.topColorId, ThemeTokens.Scenery[tokens.Sky]);
                block.SetColor(Arena.horizonColorId, ThemeTokens.Scenery[tokens.Horizon]);
            }
            else
            {
                block.SetColor(Arena.colorId, Arena.ColorFor(part.Role, palette));
            }
            part.Renderer.SetPropertyBlock(block);
        }
    }
    #endregion

    private static readonly int colorId = Shader.PropertyToID("_Color");
    private static readonly int topColorId = Shader.PropertyToID("_TopColor");
    private static readonly int horizonColorId = Shader.PropertyToID("_HorizonColor");

    private void OnEnable()
    {
        this.Apply(Application.isPlaying ? LevelPresentationCatalog.Get(LevelManager.LevelInfo).ArenaPalette : this.editorPalette);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            this.Apply(this.editorPalette);
        }
    }
}
