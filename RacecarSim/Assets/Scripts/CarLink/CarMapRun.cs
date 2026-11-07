using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// What a map run is waiting for.
/// </summary>
public enum CarMapRunStep
{
    Idle,
    Starting,
    Saving,
    Stopping,

    /// <summary>
    /// Fetching the saved map's files from the car.
    /// </summary>
    Downloading
}

/// <summary>
/// Mapping controls for a connected car through its HTTP API: polls the map service state, sends
/// start, save, and stop, waits for each job to finish, and downloads a saved map's files into a
/// folder per map.
/// </summary>
/// <remarks>
/// The car owns the run: a run started on the car (its page or command line) is shown and never
/// stopped or saved from here, and a dropped link leaves the run going. Save and Stop act only on
/// the run this object started. Call Tick every frame; nothing blocks.
/// </remarks>
public sealed class CarMapRun
{
    #region Constants
    /// <summary>
    /// Time between state requests (in seconds).
    /// </summary>
    public const double PollInterval = 1;

    /// <summary>
    /// Longest wait for the car to finish a map command (in seconds).
    /// </summary>
    public const double JobTimeout = 120;

    /// <summary>
    /// Wait before a failed download resumes (in seconds).
    /// </summary>
    public const double RetryInterval = 5;

    /// <summary>
    /// Download attempts before giving up; each resumes the partial files of the last.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Shown when a map name has characters the car refuses.
    /// </summary>
    public const string NameHint = "Use letters, digits, - and _ in the name.";
    #endregion

    #region Public Interface
    /// <summary>
    /// The newest map service state, or null before the first answer.
    /// </summary>
    public ApiState State { get; private set; }

    /// <summary>
    /// False while state requests fail; State then holds the last answer.
    /// </summary>
    public bool IsReachable { get; private set; }

    /// <summary>
    /// The map this object started, or null.
    /// </summary>
    public string OwnedMap { get; private set; }

    public CarMapRunStep Step { get; private set; }

    /// <summary>
    /// The outcome of the last command when it needs saying (a failure), else null.
    /// </summary>
    public string Message { get; private set; }

    /// <summary>
    /// Share of the current download done (0 to 1).
    /// </summary>
    public float DownloadProgress { get { return this.progress; } }

    /// <summary>
    /// True while a failed download waits to resume.
    /// </summary>
    public bool IsRetrying { get { return !double.IsNaN(this.retryAt); } }

    /// <summary>
    /// The map whose files were downloaded last, or null.
    /// </summary>
    public string DownloadedMap { get; private set; }

    /// <summary>
    /// Folder holding one map's downloads: the car's folder plus the map name.
    /// </summary>
    public string MapFolder(string map)
    {
        return Path.Combine(this.carFolder, map);
    }

    public bool IsMapping { get { return this.State != null && this.State.Mode == "mapping"; } }

    /// <summary>
    /// True while the car maps the run this object started.
    /// </summary>
    public bool IsOwnRun { get { return this.IsMapping && this.OwnedMap != null && this.State.Map == this.OwnedMap; } }

    public bool CanStart
    {
        get
        {
            return this.IsReachable && this.State != null && !this.State.IsWaiting && !this.IsMapping
                && !this.State.JobRunning && this.Step == CarMapRunStep.Idle;
        }
    }

    public bool CanSave { get { return this.IsReachable && this.IsOwnRun && !this.State.JobRunning && this.Step == CarMapRunStep.Idle; } }

    public bool CanStop { get { return this.CanSave; } }

    /// <param name="api">The car's API; the caller disposes it after Cancel.</param>
    /// <param name="carFolder">Folder for this car's downloads, one subfolder per map.</param>
    public CarMapRun(CarApiClient api, string carFolder)
    {
        this.api = api;
        this.carFolder = carFolder;
    }

    /// <summary>
    /// Starts a mapping run on a new map, with the car's session record.
    /// </summary>
    public void StartMapping(string name)
    {
        name = name?.Trim();
        if (!CarApiClient.IsValidName(name))
        {
            this.Message = CarMapRun.NameHint;
            return;
        }
        if (!this.CanStart)
        {
            return;
        }
        this.OwnedMap = name;
        this.Send(CarMapRunStep.Starting, new CarAction { Action = "start", Name = name, Record = "session" });
    }

    /// <summary>
    /// Saves the run's map on the car, then downloads its files.
    /// </summary>
    public void Save()
    {
        if (this.CanSave)
        {
            this.Send(CarMapRunStep.Saving, new CarAction { Action = "save", Name = this.OwnedMap });
        }
    }

    /// <summary>
    /// Ends the run on the car. Files are downloaded only after Save.
    /// </summary>
    public void Stop()
    {
        if (this.CanStop)
        {
            this.Send(CarMapRunStep.Stopping, new CarAction { Action = "stop", Name = this.OwnedMap });
        }
    }

    /// <summary>
    /// Advances requests and downloads.
    /// </summary>
    /// <param name="now">Link time (RosBridgeClient.Now).</param>
    public void Tick(double now)
    {
        this.TickAction(now);
        this.TickPoll(now);
        this.TickDownload(now);
    }

    /// <summary>
    /// Abandons requests in flight; a partial download stays for the next attempt.
    /// </summary>
    public void Cancel()
    {
        this.cancellation.Cancel();
    }

    /// <summary>
    /// A default map name from a local time, for example map_20261023_1430.
    /// </summary>
    public static string DefaultName(DateTime time)
    {
        return "map_" + time.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A folder name for a car: its hostname with anything but letters, digits, '.', '-' and '_'
    /// replaced by '_'.
    /// </summary>
    public static string CarFolderName(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "car";
        }
        StringBuilder builder = new StringBuilder(host.Length);
        foreach (char c in host.Trim())
        {
            bool keep = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
            builder.Append(keep ? c : '_');
        }
        string name = builder.ToString().Trim('.');
        return name.Length == 0 ? "car" : name;
    }

    /// <summary>
    /// True when the car reports a finished job as failed.
    /// </summary>
    public static bool IsFailedResult(string result)
    {
        return result != null
            && (result.StartsWith("fail", StringComparison.OrdinalIgnoreCase) || result.StartsWith("error", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A short reason for a failed command or download, for the panel.
    /// </summary>
    public static string Describe(Exception exception)
    {
        if (!(exception is CarApiException failure))
        {
            return "The car's map service did not answer.";
        }
        switch (failure.Failure)
        {
            case CarApiFailure.Busy:
                return "The car is running another map command.";
            case CarApiFailure.Http:
                return $"The car refused the command ({failure.StatusCode}).";
            case CarApiFailure.Checksum:
                return "A downloaded file did not match the car's copy.";
            case CarApiFailure.Format:
                return "Simulator could not read car's reply";
            case CarApiFailure.BadName:
                return CarMapRun.NameHint;
            default:
                return "The car's map service did not answer.";
        }
    }
    #endregion

    private readonly CarApiClient api;
    private readonly string carFolder;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private Task<string> actionTask;
    private bool isAwaitingJob;
    private double jobAcceptedAt;
    private Task<ApiState> pollTask;
    private double pollSentAt;
    private double nextPoll;
    private Task downloadTask;
    private string downloadMap;
    private int downloadAttempts;
    private double retryAt = double.NaN;
    private float progress;

    private void Send(CarMapRunStep step, CarAction action)
    {
        this.Step = step;
        this.Message = null;
        this.actionTask = this.api.SendActionAsync(action, this.cancellation.Token);
    }

    private void TickAction(double now)
    {
        if (this.actionTask != null && this.actionTask.IsCompleted)
        {
            if (this.actionTask.Status == TaskStatus.RanToCompletion)
            {
                Debug.Log($"Car link: {this.Step} accepted by the car (job {this.actionTask.Result})");
                this.isAwaitingJob = true;
                this.jobAcceptedAt = now;
                this.nextPoll = now;
            }
            else
            {
                Exception error = this.actionTask.Exception?.InnerException;
                Debug.LogWarning($"Car link: map command failed: {error?.Message}");
                this.Message = CarMapRun.Describe(error);
                if (this.Step == CarMapRunStep.Starting)
                {
                    this.OwnedMap = null;
                }
                this.Step = CarMapRunStep.Idle;
            }
            this.actionTask = null;
        }
        if (this.isAwaitingJob && now - this.jobAcceptedAt > CarMapRun.JobTimeout)
        {
            this.isAwaitingJob = false;
            this.Message = "The car did not finish the command.";
            this.Step = CarMapRunStep.Idle;
        }
    }

    private void TickPoll(double now)
    {
        if (this.pollTask != null)
        {
            if (!this.pollTask.IsCompleted)
            {
                return;
            }
            if (this.pollTask.Status == TaskStatus.RanToCompletion)
            {
                this.State = this.pollTask.Result;
                if (!this.IsReachable)
                {
                    Debug.Log($"Car link: map service answering (mode {this.State.Mode ?? "unknown"}, map {this.State.Map ?? "none"})");
                }
                this.IsReachable = true;
                // Only a request sent after the car accepted the command sees that command's job
                if (this.isAwaitingJob && this.pollSentAt >= this.jobAcceptedAt && !this.State.JobRunning)
                {
                    this.isAwaitingJob = false;
                    this.FinishJob(this.State.JobResult, now);
                }
            }
            else
            {
                if (this.IsReachable)
                {
                    Debug.LogWarning($"Car link: map service not answering: {this.pollTask.Exception?.InnerException?.Message}");
                }
                this.IsReachable = false;
            }
            this.pollTask = null;
        }
        if (now >= this.nextPoll && !this.cancellation.IsCancellationRequested)
        {
            this.nextPoll = now + CarMapRun.PollInterval;
            this.pollSentAt = now;
            this.pollTask = this.api.GetStateAsync(this.cancellation.Token);
        }
    }

    private void FinishJob(string result, double now)
    {
        CarMapRunStep step = this.Step;
        Debug.Log($"Car link: {step} finished on the car ({result ?? "no result"}); car mode {this.State.Mode ?? "unknown"}, map {this.State.Map ?? "none"}, ours {this.OwnedMap ?? "none"}");
        this.Step = CarMapRunStep.Idle;
        if (CarMapRun.IsFailedResult(result))
        {
            Debug.LogWarning($"Car link: map command ended with '{result}'");
            this.Message = step == CarMapRunStep.Starting ? "The car could not start mapping."
                : step == CarMapRunStep.Saving ? "The car could not save the map."
                : "The car could not stop mapping.";
            if (step == CarMapRunStep.Starting)
            {
                this.OwnedMap = null;
            }
            return;
        }
        if (step == CarMapRunStep.Saving)
        {
            this.downloadMap = this.OwnedMap;
            this.downloadAttempts = 0;
            this.StartDownload();
        }
        else if (step == CarMapRunStep.Stopping)
        {
            this.OwnedMap = null;
        }
    }

    private void StartDownload()
    {
        this.Step = CarMapRunStep.Downloading;
        this.progress = 0;
        this.retryAt = double.NaN;
        this.downloadAttempts++;
        this.downloadTask = this.DownloadAsync(this.downloadMap, this.MapFolder(this.downloadMap), this.cancellation.Token);
    }

    private void TickDownload(double now)
    {
        if (!double.IsNaN(this.retryAt) && now >= this.retryAt)
        {
            this.StartDownload();
        }
        if (this.downloadTask == null || !this.downloadTask.IsCompleted)
        {
            return;
        }
        if (this.downloadTask.Status == TaskStatus.RanToCompletion)
        {
            this.DownloadedMap = this.downloadMap;
            this.Step = CarMapRunStep.Idle;
        }
        else
        {
            Exception error = this.downloadTask.Exception?.InnerException;
            Debug.LogWarning($"Car link: download of {this.downloadMap} failed (attempt {this.downloadAttempts}): {error?.Message}");
            if (this.downloadAttempts < CarMapRun.MaxAttempts && !this.cancellation.IsCancellationRequested)
            {
                this.retryAt = now + CarMapRun.RetryInterval;
            }
            else
            {
                string reason = error is CarApiException failure && failure.Failure == CarApiFailure.Http
                    ? $"The car could not send the map files ({failure.StatusCode})."
                    : CarMapRun.Describe(error);
                this.Message = reason + " Save again to retry.";
                this.Step = CarMapRunStep.Idle;
            }
        }
        this.downloadTask = null;
    }

    /// <summary>
    /// Downloads the map image, its YAML, meta.yaml, and the session record when the car has one.
    /// </summary>
    private async Task DownloadAsync(string map, string folder, CancellationToken token)
    {
        MapEntry entry = await this.api.GetMapAsync(map, token).ConfigureAwait(false);
        List<string> files = new List<string> { map + ".pgm", map + ".yaml", "meta.yaml" };
        if (entry.HasSession)
        {
            files.Add("session.tar");
        }
        Directory.CreateDirectory(folder);
        for (int i = 0; i < files.Count; i++)
        {
            await this.api.DownloadFileAsync(map, files[i], Path.Combine(folder, files[i]), new FileProgress(this, i, files.Count), token)
                .ConfigureAwait(false);
            this.progress = (float)(i + 1) / files.Count;
        }
    }

    /// <summary>
    /// Maps one file's progress onto the whole download, on the thread that reports it.
    /// </summary>
    private sealed class FileProgress : IProgress<double>
    {
        private readonly CarMapRun owner;
        private readonly int index;
        private readonly int count;

        public FileProgress(CarMapRun owner, int index, int count)
        {
            this.owner = owner;
            this.index = index;
            this.count = count;
        }

        public void Report(double value)
        {
            this.owner.progress = (float)((this.index + value) / this.count);
        }
    }
}
