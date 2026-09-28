using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Optional measurements for scripted verification runs, enabled with -racecarsim-diagnostics
/// (see LaunchOptions). Writes one CSV row per physics step for car 0 with the true rigidbody
/// motion next to the simulated sensor readings, logs resource counts when the level loads, and
/// can take periodic screenshots and restart the level on a timer.
/// </summary>
public class SimDiagnostics : MonoBehaviour
{
    /// <summary>
    /// CSV columns, in order.
    /// </summary>
    public const string Header =
        "time,time_scale,fixed_dt,pos_x,pos_y,pos_z,yaw_deg,pitch_deg,roll_deg," +
        "vel_x,vel_y,vel_z,world_ang_x,world_ang_y,world_ang_z," +
        "imu_acc_x,imu_acc_y,imu_acc_z,imu_gyro_x,imu_gyro_y,imu_gyro_z,lidar_scans";

    private StreamWriter writer;

    private float nextScreenshotTime;

    private int screenshotIndex;

    private float restartTime = float.PositiveInfinity;

    /// <summary>
    /// Logs the counts of live textures and materials, which should stay flat across level reloads.
    /// </summary>
    public static void LogResourceCounts(string label)
    {
        Debug.Log($"Diagnostics resources [{label}]: " +
            $"Texture2D={Resources.FindObjectsOfTypeAll<Texture2D>().Length} " +
            $"RenderTexture={Resources.FindObjectsOfTypeAll<RenderTexture>().Length} " +
            $"Material={Resources.FindObjectsOfTypeAll<Material>().Length}");
    }

    private void Start()
    {
        LaunchOptions.Options options = LaunchOptions.Current;
        bool append = File.Exists(options.DiagnosticsPath);
        this.writer = new StreamWriter(options.DiagnosticsPath, append) { AutoFlush = true };
        if (!append)
        {
            this.writer.WriteLine(SimDiagnostics.Header);
        }

        SimDiagnostics.LogResourceCounts($"level {LevelManager.LevelInfo.DisplayName} loaded, frame {Time.frameCount}");
        this.nextScreenshotTime = Time.unscaledTime + options.ScreenshotInterval;
        if (options.RestartInterval > 0)
        {
            this.restartTime = Time.unscaledTime + options.RestartInterval;
        }
    }

    private void Update()
    {
        LaunchOptions.Options options = LaunchOptions.Current;
        if (options.ScreenshotInterval > 0 && Time.unscaledTime >= this.nextScreenshotTime)
        {
            string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.DiagnosticsPath)), $"screenshot-{this.screenshotIndex++:D3}.png");
            ScreenCapture.CaptureScreenshot(path);
            this.nextScreenshotTime = Time.unscaledTime + options.ScreenshotInterval;
        }

        if (Time.unscaledTime >= this.restartTime)
        {
            this.restartTime = float.PositiveInfinity;
            LevelManager.RestartLevel();
        }
    }

    private void FixedUpdate()
    {
        Racecar car = LevelManager.GetCar(0);
        if (car == null || this.writer == null)
        {
            return;
        }

        Rigidbody body = car.GetComponent<Rigidbody>();
        Vector3 euler = car.transform.eulerAngles;
        Vector3 velocity = body.linearVelocity / 10;
        Vector3 angular = body.angularVelocity;
        Vector3 acc = car.Physics.LinearAcceleration;
        Vector3 gyro = car.Physics.AngularVelocity;
        Vector3 pos = car.transform.position / 10;

        float[] values =
        {
            Time.time, Time.timeScale, Time.fixedDeltaTime, pos.x, pos.y, pos.z, euler.y, euler.x, euler.z,
            velocity.x, velocity.y, velocity.z, angular.x, angular.y, angular.z,
            acc.x, acc.y, acc.z, gyro.x, gyro.y, gyro.z, car.Lidar.CompletedScans
        };
        this.writer.WriteLine(string.Join(",", System.Array.ConvertAll(values, v => v.ToString("R", CultureInfo.InvariantCulture))));
    }

    private void OnDestroy()
    {
        this.writer?.Dispose();
        this.writer = null;
    }
}
