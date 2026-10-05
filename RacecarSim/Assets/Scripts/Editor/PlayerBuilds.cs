using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

/// <summary>
/// Builds Windows, macOS, and Linux players from the enabled scenes in the build settings, and
/// compiles player scripts per platform without building. The version comes from
/// PlayerSettings.bundleVersion, which the main menu also displays.
/// </summary>
/// <remarks>
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod PlayerBuilds.BuildAllBatch [-racecarsim-build-output DIR]</code>
/// <code>Unity -batchmode -quit -projectPath . -executeMethod PlayerBuilds.CompileAllBatch</code>
/// </remarks>
public static class PlayerBuilds
{
    /// <summary>
    /// The platforms built for a release: target, output folder name, and executable name.
    /// </summary>
    public static readonly (BuildTarget Target, string Folder, string Executable)[] Platforms =
    {
        (BuildTarget.StandaloneWindows64, "Windows", "RacecarSim.exe"),
        (BuildTarget.StandaloneOSX, "Mac", "RacecarSim.app"),
        (BuildTarget.StandaloneLinux64, "Linux", "RacecarSim.x86_64"),
    };

    /// <summary>
    /// Command-line option overriding the build output root (default: Builds/ in the project).
    /// </summary>
    private const string outputOption = "-racecarsim-build-output";

    /// <summary>
    /// The game-code assembly (Assets/Scripts/RacecarSim.asmdef); its presence marks a successful script compile.
    /// </summary>
    private const string runtimeAssembly = "RacecarSim.dll";

    [MenuItem("RacecarSim/Build All Players")]
    public static void BuildAll()
    {
        PlayerBuilds.Build(PlayerBuilds.OutputRoot());
    }

    [MenuItem("RacecarSim/Compile Player Scripts (All Platforms)")]
    public static void CompileAll()
    {
        PlayerBuilds.CompileScripts();
    }

    /// <summary>
    /// Batch entry point for BuildAll; exits with 1 if any platform fails.
    /// </summary>
    public static void BuildAllBatch()
    {
        EditorApplication.Exit(PlayerBuilds.Build(PlayerBuilds.OutputRoot()) ? 0 : 1);
    }

    /// <summary>
    /// Batch entry point for CompileAll; exits with 1 if any platform fails.
    /// </summary>
    public static void CompileAllBatch()
    {
        EditorApplication.Exit(PlayerBuilds.CompileScripts() ? 0 : 1);
    }

    /// <summary>
    /// Builds every platform into root/v{version}/{platform}. Logs one result line per platform.
    /// </summary>
    /// <param name="root">The output root folder.</param>
    /// <returns>True if every build succeeded.</returns>
    public static bool Build(string root)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        string versionFolder = Path.Combine(root, $"v{PlayerSettings.bundleVersion}");
        bool allSucceeded = true;
        BuildManifest manifest = new BuildManifest()
        {
            version = PlayerSettings.bundleVersion,
            commit = PlayerBuilds.Git("rev-parse HEAD"),
            treeClean = PlayerBuilds.Git("status --porcelain") == string.Empty,
            builtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            keyEmbedded = AutograderKeyBuildStep.HasValidKey,
        };

        foreach ((BuildTarget target, string folder, string executable) in PlayerBuilds.Platforms)
        {
            BuildPlayerOptions options = new BuildPlayerOptions()
            {
                scenes = scenes,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                locationPathName = Path.Combine(versionFolder, folder, executable),
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool succeeded = report.summary.result == BuildResult.Succeeded;
            allSucceeded &= succeeded;
            Debug.Log($"Player build [{target}] v{PlayerSettings.bundleVersion}: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / 1e6:F0} MB, {report.summary.totalTime.TotalSeconds:F0} s -> {options.locationPathName}");
            manifest.platforms.Add($"{folder}: {report.summary.result}");
        }

        File.WriteAllText(Path.Combine(versionFolder, PlayerBuilds.ManifestFile), JsonUtility.ToJson(manifest, true));
        return allSucceeded;
    }

    /// <summary>
    /// Name of the file, next to the platform folders, that records how the players were built.
    /// </summary>
    public const string ManifestFile = "build.json";

    /// <summary>
    /// Contents of ManifestFile.
    /// </summary>
    [Serializable]
    private class BuildManifest
    {
        public string version;
        public string commit;
        public bool treeClean;
        public string builtUtc;
        public bool keyEmbedded;
        public List<string> platforms = new List<string>();
    }

    /// <summary>
    /// Runs git in the project folder and returns its trimmed output, or "unknown" if git is unavailable.
    /// </summary>
    private static string Git(string arguments)
    {
        try
        {
            ProcessStartInfo start = new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : "unknown";
            }
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Compiles player scripts for every platform into a temporary folder, without building.
    /// Logs one result line per platform.
    /// </summary>
    /// <returns>True if every platform compiled.</returns>
    public static bool CompileScripts()
    {
        bool allSucceeded = true;
        foreach ((BuildTarget target, string _, string _) in PlayerBuilds.Platforms)
        {
            string output = Path.Combine(Path.GetTempPath(), "racecarsim-player-scripts", target.ToString());
            Directory.CreateDirectory(output);

            ScriptCompilationSettings settings = new ScriptCompilationSettings()
            {
                target = target,
                group = BuildTargetGroup.Standalone,
                options = ScriptCompilationOptions.None,
            };
            // One retry: the editor may hold a lock on a script assembly it is refreshing at the same time
            ScriptCompilationResult result = default;
            bool succeeded = false;
            for (int attempt = 1; attempt <= 2 && !succeeded; attempt++)
            {
                result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
                succeeded = result.assemblies != null && result.assemblies.Contains(PlayerBuilds.runtimeAssembly);
                if (!succeeded && attempt == 1)
                {
                    Debug.LogWarning($"Player scripts [{target}]: attempt 1 failed; retrying.");
                }
            }
            allSucceeded &= succeeded;
            Debug.Log($"Player scripts [{target}]: {(succeeded ? "compiled" : "FAILED")}, {result.assemblies?.Count ?? 0} assemblies");
        }
        return allSucceeded;
    }

    private static string OutputRoot()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, PlayerBuilds.outputOption);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : Path.Combine(Directory.GetCurrentDirectory(), "Builds");
    }
}
