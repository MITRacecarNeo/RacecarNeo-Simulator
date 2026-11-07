using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the arena scenery (Prefabs/Arena) and nests it in the shared Terrain prefab, so every
/// scene that places Terrain.prefab gets it with no scene edit. Meshes are generated in the flat
/// illustration style: one color per part, a fixed shade per face in the vertex color.
/// </summary>
/// <remarks>
/// The shared terrain is 500 x 500 with its corner at (-250, 0, -250): a flat floor out to about
/// 205 from the center, cliffs, and a plateau at height 50 from about 212 to the edge (Survey).
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod ArenaSetup.Apply</code>
/// <code>Unity -batchmode -quit -projectPath . -executeMethod ArenaSetup.Preview</code>
/// </remarks>
public static class ArenaSetup
{
    private const string terrainPrefabPath = "Assets/Prefabs/Terrain.prefab";
    private const string folder = "Assets/Prefabs/Arena";
    private const string prefabPath = ArenaSetup.folder + "/Arena.prefab";
    private const string meshPath = ArenaSetup.folder + "/ArenaMeshes.asset";
    private const string flatMaterialPath = ArenaSetup.folder + "/ArenaFlat.mat";
    private const string skyMaterialPath = ArenaSetup.folder + "/ArenaSky.mat";
    private const string previewScenePath = "Assets/Scenes/Demo.unity";

    // Arena center in Terrain.prefab space (the prefab root is the terrain corner)
    private static readonly Vector3 center = new Vector3(250, 0, 250);

    private const float floorEdge = 203;
    private const float plateauHeight = 50;
    private const float plateauInner = 216;
    private const float terrainHalf = 250;
    private const float skyRadius = 600;
    private const float groundRadius = 590;

    // Direction the face shades are computed from; top faces come out near full color
    private static readonly Vector3 shadeLight = new Vector3(0.35f, 1f, 0.55f).normalized;

    /// <summary>
    /// Writes the meshes, materials, and Arena prefab, and nests the prefab in Terrain.prefab.
    /// </summary>
    public static void Apply()
    {
        Directory.CreateDirectory(ArenaSetup.folder);
        Material flat = ArenaSetup.SaveMaterial(ArenaSetup.flatMaterialPath, "RacecarSim/ArenaFlat");
        Material sky = ArenaSetup.SaveMaterial(ArenaSetup.skyMaterialPath, "RacecarSim/ArenaSky");

        Dictionary<string, (Mesh Mesh, Arena.Role Role)> meshes = ArenaSetup.BuildMeshes();
        Mesh first = null;
        foreach ((Mesh mesh, Arena.Role _) in meshes.Values)
        {
            if (first == null)
            {
                AssetDatabase.CreateAsset(mesh, ArenaSetup.meshPath);
                first = mesh;
            }
            else
            {
                AssetDatabase.AddObjectToAsset(mesh, first);
            }
        }
        AssetDatabase.SaveAssets();

        int layer = LayerMask.NameToLayer(Arena.LayerName);
        if (layer < 0)
        {
            throw new System.InvalidOperationException($"No {Arena.LayerName} layer in ProjectSettings/TagManager.asset");
        }
        GameObject root = new GameObject("Arena") { layer = layer };
        try
        {
            List<Arena.Part> parts = new List<Arena.Part>();
            foreach (KeyValuePair<string, (Mesh Mesh, Arena.Role Role)> entry in meshes)
            {
                GameObject child = new GameObject(entry.Key) { layer = layer };
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = entry.Value.Mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = entry.Value.Role == Arena.Role.Sky ? sky : flat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                parts.Add(new Arena.Part { Renderer = renderer, Role = entry.Value.Role });
            }
            Arena arena = root.AddComponent<Arena>();
            SerializedObject serialized = new SerializedObject(arena);
            SerializedProperty list = serialized.FindProperty("parts");
            list.arraySize = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("Renderer").objectReferenceValue = parts[i].Renderer;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("Role").enumValueIndex = (int)parts[i].Role;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ArenaSetup.prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        ArenaSetup.NestInTerrain();
        Debug.Log($"ArenaSetup: {ArenaSetup.prefabPath} written ({meshes.Count} parts, {meshes.Values.Sum(m => m.Mesh.vertexCount)} vertices) and nested in {ArenaSetup.terrainPrefabPath}");
    }

    /// <summary>
    /// Renders the preview scene with each palette from a floor view and a high corner view, and
    /// the car's color camera with and without the arena, to Logs/arena-preview-*.png.
    /// </summary>
    public static void Preview()
    {
        EditorSceneManager.OpenScene(ArenaSetup.previewScenePath, OpenSceneMode.Single);
        Arena arena = Object.FindAnyObjectByType<Arena>();
        Camera camera = new GameObject("ArenaPreviewCamera").AddComponent<Camera>();
        camera.fieldOfView = 60;
        camera.farClipPlane = 1000;
        try
        {
            (string Name, Vector3 Position, Vector3 Target)[] views =
            {
                ("floor", new Vector3(0, 6, -60), new Vector3(0, 30, 200)),
                ("corner", new Vector3(-150, 70, -150), new Vector3(60, 0, 60)),
            };
            foreach (ArenaPalette palette in System.Enum.GetValues(typeof(ArenaPalette)))
            {
                arena.Apply(palette);
                foreach ((string name, Vector3 position, Vector3 target) in views)
                {
                    camera.transform.position = position;
                    camera.transform.LookAt(target);
                    ArenaSetup.RenderToFile(camera, $"arena-preview-{palette.ToString().ToLowerInvariant()}-{name}.png");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(camera.gameObject);
        }
    }

    /// <summary>
    /// Logs the shared terrain's size and its height profile from the center to each edge.
    /// </summary>
    public static void Survey()
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaSetup.terrainPrefabPath).GetComponent<Terrain>().terrainData;
        StringBuilder log = new StringBuilder();
        log.AppendLine($"ArenaSetup.Survey: size {data.size}, heightmap {data.heightmapResolution}");
        foreach ((string name, Vector2 direction) in new[] { ("+x", Vector2.right), ("-x", Vector2.left), ("+z", Vector2.up), ("-z", Vector2.down) })
        {
            log.Append($"  {name}:");
            for (float distance = 180; distance <= 250; distance += 5)
            {
                Vector2 point = new Vector2(data.size.x, data.size.z) * 0.5f + direction * distance;
                float height = data.GetInterpolatedHeight(point.x / data.size.x, point.y / data.size.z);
                log.Append($" {distance.ToString(CultureInfo.InvariantCulture)}={height.ToString("F1", CultureInfo.InvariantCulture)}");
            }
            log.AppendLine();
        }
        Debug.Log(log.ToString());
    }

    private static void NestInTerrain()
    {
        GameObject terrain = PrefabUtility.LoadPrefabContents(ArenaSetup.terrainPrefabPath);
        try
        {
            Transform existing = terrain.transform.Find("Arena");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
            GameObject arena = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArenaSetup.prefabPath), terrain.transform);
            arena.transform.localPosition = ArenaSetup.center;
            PrefabUtility.SaveAsPrefabAsset(terrain, ArenaSetup.terrainPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(terrain);
        }
    }

    private static Material SaveMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find(shaderName);
        material.enableInstancing = false;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Dictionary<string, (Mesh, Arena.Role)> BuildMeshes()
    {
        Dictionary<string, (Mesh, Arena.Role)> meshes = new Dictionary<string, (Mesh, Arena.Role)>
        {
            ["Sky"] = (ArenaSetup.Sky(), Arena.Role.Sky),
            ["Ground"] = (ArenaSetup.Ground(), Arena.Role.Ground),
        };

        System.Random random = new System.Random(2949);
        (string Name, Arena.Role Role, int Count, float MinRadius, float MaxRadius, float MinHeight, float MaxHeight)[] bands =
        {
            ("MountainsFar", Arena.Role.MountainFar, 28, 500, 530, 150, 210),
            ("MountainsMid", Arena.Role.MountainMid, 24, 440, 480, 110, 170),
            ("MountainsNear", Arena.Role.MountainNear, 20, 385, 420, 70, 120),
        };
        foreach (var band in bands)
        {
            MeshBuilder builder = new MeshBuilder();
            for (int i = 0; i < band.Count; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / band.Count * Mathf.PI * 2;
                float radius = Mathf.Lerp(band.MinRadius, band.MaxRadius, (float)random.NextDouble());
                float height = Mathf.Lerp(band.MinHeight, band.MaxHeight, (float)random.NextDouble());
                float width = height * Mathf.Lerp(0.5f, 0.7f, (float)random.NextDouble());
                Vector3 baseCenter = new Vector3(Mathf.Sin(angle) * radius, ArenaSetup.plateauHeight - 2, Mathf.Cos(angle) * radius);
                builder.AddPeak(baseCenter, width, height, 6, (float)random.NextDouble() * 60);
            }
            meshes[band.Name] = (builder.ToMesh(band.Name), band.Role);
        }

        // Grandstands on the plateau, one per side, leaving the corners to the light towers
        MeshBuilder seats = new MeshBuilder();
        MeshBuilder risers = new MeshBuilder();
        MeshBuilder backs = new MeshBuilder();
        MeshBuilder fascia = new MeshBuilder();
        const int tiers = 8;
        const float tierDepth = 3.5f;
        const float tierHeight = 2.5f;
        const float standHalfLength = 165;
        float back = ArenaSetup.plateauInner + tiers * tierDepth;
        for (int side = 0; side < 4; side++)
        {
            Quaternion facing = Quaternion.Euler(0, side * 90, 0);
            for (int k = 0; k < tiers; k++)
            {
                float front = ArenaSetup.plateauInner + k * tierDepth;
                float top = ArenaSetup.plateauHeight + (k + 1) * tierHeight;
                MeshBuilder target = k % 2 == 0 ? seats : risers;
                target.AddBox(facing, new Vector3(0, (ArenaSetup.plateauHeight - 1 + top) / 2, (front + back) / 2),
                    new Vector3(standHalfLength * 2, top - ArenaSetup.plateauHeight + 1, back - front));
            }
            float wallTop = ArenaSetup.plateauHeight + tiers * tierHeight + 7;
            backs.AddBox(facing, new Vector3(0, (ArenaSetup.plateauHeight - 1 + wallTop) / 2, back + 1),
                new Vector3(standHalfLength * 2 + 4, wallTop - ArenaSetup.plateauHeight + 1, 2));
            // Brand band along the front edge of the stand
            fascia.AddBox(facing, new Vector3(0, ArenaSetup.plateauHeight + 1.5f, ArenaSetup.plateauInner - 0.5f), new Vector3(standHalfLength * 2, 3, 1));
            // Canopy over the back rows
            backs.AddBox(facing, new Vector3(0, wallTop + 0.5f, back - 6), new Vector3(standHalfLength * 2 + 4, 1, 14));
        }
        meshes["StandsSeats"] = (seats.ToMesh("StandsSeats"), Arena.Role.Seat);
        meshes["StandsRisers"] = (risers.ToMesh("StandsRisers"), Arena.Role.StructureDark);
        meshes["StandsBacks"] = (backs.ToMesh("StandsBacks"), Arena.Role.Structure);
        meshes["StandsFascia"] = (fascia.ToMesh("StandsFascia"), Arena.Role.Fascia);

        // Light towers at the plateau corners, lamp heads facing the center
        MeshBuilder poles = new MeshBuilder();
        MeshBuilder lamps = new MeshBuilder();
        for (int corner = 0; corner < 4; corner++)
        {
            Quaternion facing = Quaternion.Euler(0, 45 + corner * 90, 0);
            Vector3 position = facing * new Vector3(0, 0, 330);
            poles.AddBox(Quaternion.identity, new Vector3(position.x, ArenaSetup.plateauHeight + 35, position.z), new Vector3(4, 72, 4));
            lamps.AddBox(facing, new Vector3(0, ArenaSetup.plateauHeight + 72, 328), new Vector3(16, 7, 3));
        }
        meshes["TowerPoles"] = (poles.ToMesh("TowerPoles"), Arena.Role.Structure);
        meshes["TowerLamps"] = (lamps.ToMesh("TowerLamps"), Arena.Role.Lamp);

        // Kerb barrier at the foot of the cliffs, alternating red and white blocks
        MeshBuilder red = new MeshBuilder();
        MeshBuilder white = new MeshBuilder();
        const float block = 5;
        int perSide = Mathf.RoundToInt(ArenaSetup.floorEdge * 2 / block);
        for (int side = 0; side < 4; side++)
        {
            Quaternion facing = Quaternion.Euler(0, side * 90, 0);
            for (int i = 0; i < perSide; i++)
            {
                float along = -ArenaSetup.floorEdge + (i + 0.5f) * block;
                (i % 2 == 0 ? red : white).AddBox(facing, new Vector3(along, 0.8f, ArenaSetup.floorEdge), new Vector3(block, 1.6f, 1.2f));
            }
        }
        meshes["KerbRed"] = (red.ToMesh("KerbRed"), Arena.Role.KerbRed);
        meshes["KerbWhite"] = (white.ToMesh("KerbWhite"), Arena.Role.KerbWhite);
        return meshes;
    }

    private static Mesh Sky()
    {
        Mesh mesh = new Mesh { name = "Sky" };
        const int rings = 16;
        const int segments = 32;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float polar = Mathf.PI * r / rings;
            for (int s = 0; s <= segments; s++)
            {
                float azimuth = Mathf.PI * 2 * s / segments;
                vertices.Add(new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth)) * ArenaSetup.skyRadius);
            }
        }
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s;
                int b = a + segments + 1;
                triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Flat ground outside the terrain: from the terrain's square edge out to a circle, just
    /// below the plateau.
    /// </summary>
    private static Mesh Ground()
    {
        MeshBuilder builder = new MeshBuilder();
        const int segments = 128;
        float y = ArenaSetup.plateauHeight - 1;
        for (int s = 0; s < segments; s++)
        {
            Vector3 a0 = ArenaSetup.GroundRingPoint(s, segments, false, y);
            Vector3 a1 = ArenaSetup.GroundRingPoint(s + 1, segments, false, y);
            Vector3 b0 = ArenaSetup.GroundRingPoint(s, segments, true, y);
            Vector3 b1 = ArenaSetup.GroundRingPoint(s + 1, segments, true, y);
            builder.AddTriangle(a0, b0, a1);
            builder.AddTriangle(a1, b0, b1);
        }
        return builder.ToMesh("Ground");
    }

    private static Vector3 GroundRingPoint(int index, int segments, bool outer, float y)
    {
        float angle = Mathf.PI * 2 * index / segments;
        Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        float distance = outer ? ArenaSetup.groundRadius : ArenaSetup.terrainHalf / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        return new Vector3(direction.x * distance, y, direction.y * distance);
    }

    private static void RenderToFile(Camera camera, string file)
    {
        RenderTexture target = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
        RenderTexture.active = null;
        camera.targetTexture = null;
        File.WriteAllBytes(Path.Combine("Logs", file), pixels.EncodeToPNG());
        Object.DestroyImmediate(pixels);
        target.Release();
        Object.DestroyImmediate(target);
    }

    private static float Shade(Vector3 normal)
    {
        return 0.68f + 0.32f * Mathf.Max(0, Vector3.Dot(normal.normalized, ArenaSetup.shadeLight));
    }

    /// <summary>
    /// Collects flat-shaded triangles: each face has its own vertices, with the face shade in the
    /// vertex color.
    /// </summary>
    private class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> triangles = new List<int>();

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            byte shade = (byte)Mathf.RoundToInt(255 * ArenaSetup.Shade(Vector3.Cross(b - a, c - a)));
            int start = this.vertices.Count;
            this.vertices.AddRange(new[] { a, b, c });
            this.colors.AddRange(Enumerable.Repeat(new Color32(shade, shade, shade, 255), 3));
            this.triangles.AddRange(new[] { start, start + 1, start + 2 });
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            this.AddTriangle(a, b, c);
            this.AddTriangle(a, c, d);
        }

        /// <summary>
        /// A box at a center (in the frame rotated by facing) with the given size; faces point out.
        /// </summary>
        public void AddBox(Quaternion facing, Vector3 boxCenter, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => facing * (boxCenter + new Vector3(x * h.x, y * h.y, z * h.z));
            this.AddQuad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            this.AddQuad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
            this.AddQuad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
            this.AddQuad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));
            this.AddQuad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));
            this.AddQuad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
        }

        /// <summary>
        /// A faceted peak: a ring of base points around a center and an apex offset toward one side.
        /// </summary>
        public void AddPeak(Vector3 baseCenter, float width, float height, int sides, float twistDegrees)
        {
            Vector3 apex = baseCenter + new Vector3(width * 0.12f, height, -width * 0.08f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (twistDegrees + 360f * i / sides) * Mathf.Deg2Rad;
                float a1 = (twistDegrees + 360f * (i + 1) / sides) * Mathf.Deg2Rad;
                float r0 = width * (0.8f + 0.2f * Mathf.Sin(i * 2.3f));
                float r1 = width * (0.8f + 0.2f * Mathf.Sin((i + 1) * 2.3f));
                Vector3 p0 = baseCenter + new Vector3(Mathf.Sin(a0) * r0, 0, Mathf.Cos(a0) * r0);
                Vector3 p1 = baseCenter + new Vector3(Mathf.Sin(a1) * r1, 0, Mathf.Cos(a1) * r1);
                this.AddTriangle(p1, apex, p0);
            }
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (this.vertices.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(this.vertices);
            mesh.SetColors(this.colors);
            mesh.SetTriangles(this.triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
