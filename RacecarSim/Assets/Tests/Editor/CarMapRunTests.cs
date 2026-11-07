using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NUnit.Framework;

/// <summary>
/// Mapping controls against a loopback map service: start, save, and stop from the sim; downloads
/// that match the car's files; a run started on the car shown but not controlled; the API dropping
/// mid-run; the session recorder's rows.
/// </summary>
public class CarMapRunTests
{
    /// <summary>
    /// A map service as the car's API answers it: one job at a time, each finishing after a delay.
    /// </summary>
    private sealed class FakeMapService
    {
        public string Mode = "off";
        public string Map;
        public bool JobRunning;
        public string JobAction;
        public string JobClient;
        public string JobResult;
        public string FailNextResult;
        public bool IsDown;
        public bool IsBusy;
        public int FailFileRequests;
        public readonly List<string> Actions = new List<string>();
        public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double jobEndsAt;
        private string pendingAction;
        private string pendingName;

        public CarLinkLoopbackServer.Response Handle(CarLinkLoopbackServer.Request request)
        {
            lock (this)
            {
                if (this.IsDown)
                {
                    return CarLinkLoopbackServer.Response.Json("{\"ok\": false, \"message\": \"down\"}", 503, "Service Unavailable");
                }
                this.Finish();
                if (request.Path == "/api/v1/state")
                {
                    string map = this.Map == null ? "null" : $"\"{this.Map}\"";
                    string client = this.JobClient == null ? "null" : $"\"{this.JobClient}\"";
                    string result = this.JobResult == null ? "null" : $"\"{this.JobResult}\"";
                    string action = this.JobAction == null ? "null" : $"\"{this.JobAction}\"";
                    string pose = this.Mode == "off" ? "null" : "[0.0, 0.0, 0.0]";
                    return CarLinkLoopbackServer.Response.Json(
                        $"{{\"mode\": \"{this.Mode}\", \"map\": {map}, \"localized\": true, \"pose\": {pose}, "
                        + $"\"job\": {{\"running\": {(this.JobRunning ? "true" : "false")}, \"action\": {action}, \"client\": {client}, \"started\": 1.0, \"result\": {result}}}, "
                        + "\"twin\": false, \"link_clients\": 1}");
                }
                if (request.Path == "/api/v1/action" && request.Method == "POST")
                {
                    this.Actions.Add(request.Body);
                    if (this.JobRunning || this.IsBusy)
                    {
                        return CarLinkLoopbackServer.Response.Json("{\"ok\": false, \"message\": \"busy\"}", 409, "Conflict");
                    }
                    CarLinkValue body = CarLinkJson.Parse(request.Body);
                    this.JobRunning = true;
                    this.JobAction = body["action"].AsString();
                    this.JobClient = body["client"].AsString();
                    this.JobResult = null;
                    this.pendingAction = this.JobAction;
                    this.pendingName = body["name"].IsNull ? null : body["name"].AsString();
                    this.jobEndsAt = this.clock.Elapsed.TotalSeconds + 0.3;
                    return CarLinkLoopbackServer.Response.Json("{\"ok\": true, \"job\": \"job-1\"}");
                }
                if (request.Path == "/api/v1/maps/" + this.Map)
                {
                    return CarLinkLoopbackServer.Response.Json($"{{\"name\": \"{this.Map}\", \"saved\": true, \"session\": true, \"meta\": {{}}}}");
                }
                string prefix = $"/api/v1/maps/{this.Map}/files/";
                if (request.Path.StartsWith(prefix) && this.Files.TryGetValue(request.Path.Substring(prefix.Length), out byte[] file))
                {
                    return this.Serve(request, file);
                }
                return null;
            }
        }

        /// <summary>
        /// Starts a run as the car's own page would.
        /// </summary>
        public void StartFromPage(string name)
        {
            lock (this)
            {
                this.Mode = "mapping";
                this.Map = name;
                this.JobAction = "start";
                this.JobClient = "mapview";
                this.JobResult = "ok";
            }
        }

        private CarLinkLoopbackServer.Response Serve(CarLinkLoopbackServer.Request request, byte[] file)
        {
            CarLinkLoopbackServer.Response response = new CarLinkLoopbackServer.Response { Body = file };
            using (SHA256 sha = SHA256.Create())
            {
                response.Headers["X-Content-SHA256"] = string.Concat(sha.ComputeHash(file).Select(b => b.ToString("x2")));
            }
            response.Headers["Accept-Ranges"] = "bytes";
            if (request.Headers.TryGetValue("Range", out string range))
            {
                int start = int.Parse(range.Substring("bytes=".Length).TrimEnd('-'));
                response.Status = 206;
                response.Reason = "Partial Content";
                response.Body = file.Skip(start).ToArray();
                response.Headers["Content-Range"] = $"bytes {start}-{file.Length - 1}/{file.Length}";
            }
            else if (this.FailFileRequests > 0)
            {
                this.FailFileRequests--;
                return CarLinkLoopbackServer.Response.Json("{\"ok\": false, \"message\": \"archive not ready\"}", 500, "Internal Server Error");
            }
            return response;
        }

        private void Finish()
        {
            if (!this.JobRunning || this.clock.Elapsed.TotalSeconds < this.jobEndsAt)
            {
                return;
            }
            this.JobRunning = false;
            if (this.FailNextResult != null)
            {
                this.JobResult = this.FailNextResult;
                this.FailNextResult = null;
                return;
            }
            this.JobResult = "ok";
            switch (this.pendingAction)
            {
                case "start":
                    this.Mode = "mapping";
                    this.Map = this.pendingName;
                    this.Files.Clear();
                    break;
                case "save":
                    this.Files[this.Map + ".pgm"] = CarMapRunTests.Bytes("P5\n4 4\n255\n", 40000);
                    this.Files[this.Map + ".yaml"] = Encoding.ASCII.GetBytes($"image: {this.Map}.pgm\nresolution: 0.05\n");
                    this.Files["meta.yaml"] = Encoding.ASCII.GetBytes($"name: {this.Map}\n");
                    this.Files["session.tar"] = CarMapRunTests.Bytes("tar", 150000);
                    break;
                case "stop":
                    this.Mode = "off";
                    break;
            }
        }
    }

    private CarLinkLoopbackServer server;
    private FakeMapService service;
    private CarApiClient api;
    private CarMapRun run;
    private string folder;
    private Stopwatch clock;
    private double skipped;

    [SetUp]
    public void SetUp()
    {
        this.server = new CarLinkLoopbackServer();
        this.service = new FakeMapService();
        this.server.Http = this.service.Handle;
        this.api = new CarApiClient("127.0.0.1", this.server.Port);
        this.folder = Path.Combine(Path.GetTempPath(), "racecarsim-maprun-" + Guid.NewGuid().ToString("N"));
        this.run = new CarMapRun(this.api, this.folder);
        this.clock = Stopwatch.StartNew();
        this.skipped = 0;
    }

    [TearDown]
    public void TearDown()
    {
        this.run.Cancel();
        this.api.Dispose();
        this.server.Dispose();
        if (Directory.Exists(this.folder))
        {
            Directory.Delete(this.folder, true);
        }
    }

    private double Now { get { return this.clock.Elapsed.TotalSeconds + this.skipped; } }

    /// <summary>
    /// Ticks the run until a condition holds, as frames would.
    /// </summary>
    private void TickUntil(Func<bool> condition, string what, double seconds = 5)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (!condition())
        {
            if (wait.Elapsed.TotalSeconds > seconds)
            {
                Assert.Fail($"timed out waiting for {what}; step {this.run.Step}, retrying {this.run.IsRetrying}, message {this.run.Message}, file failures left {this.service.FailFileRequests}");
            }
            this.run.Tick(this.Now);
            Thread.Sleep(10);
        }
    }

    private static byte[] Bytes(string start, int length)
    {
        byte[] data = new byte[length];
        Encoding.ASCII.GetBytes(start).CopyTo(data, 0);
        for (int i = start.Length; i < length; i++)
        {
            data[i] = (byte)(i * 31 % 251);
        }
        return data;
    }

    [Test]
    public void StartSaveStop_DownloadsMatchTheCarsFiles()
    {
        this.TickUntil(() => this.run.CanStart, "the first state");
        Assert.AreEqual("Not mapping", CarWorld.RunText(this.run));

        this.run.StartMapping("room_1");
        Assert.AreEqual(CarMapRunStep.Starting, this.run.Step);
        Assert.AreEqual("Starting", CarWorld.RunText(this.run));
        this.TickUntil(() => this.run.IsOwnRun && this.run.Step == CarMapRunStep.Idle, "the run");
        CarLinkValue start = CarLinkJson.Parse(this.service.Actions[0]);
        Assert.AreEqual("start", start["action"].AsString());
        Assert.AreEqual("room_1", start["name"].AsString());
        Assert.AreEqual("session", start["record"].AsString());
        Assert.AreEqual(CarApiClient.ClientName, start["client"].AsString());
        Assert.IsTrue(start["confirm"].AsBool());
        Assert.IsFalse(this.run.CanStart);
        Assert.IsTrue(this.run.CanSave);
        Assert.AreEqual("Mapping room_1", CarWorld.RunText(this.run));

        this.run.Save();
        this.TickUntil(() => this.run.DownloadedMap == "room_1" && this.run.Step == CarMapRunStep.Idle, "the download");
        string mapFolder = this.run.MapFolder("room_1");
        foreach (string file in new[] { "room_1.pgm", "room_1.yaml", "meta.yaml", "session.tar" })
        {
            CollectionAssert.AreEqual(this.service.Files[file], File.ReadAllBytes(Path.Combine(mapFolder, file)), file);
        }
        Assert.AreEqual(1, this.run.DownloadProgress, 1e-6);
        Assert.AreEqual("Mapping room_1, saved", CarWorld.RunText(this.run));

        this.run.Stop();
        this.TickUntil(() => !this.run.IsMapping && this.run.Step == CarMapRunStep.Idle, "the stop");
        Assert.IsNull(this.run.OwnedMap);
        Assert.AreEqual("Saved room_1", CarWorld.RunText(this.run));
        CollectionAssert.AreEqual(new[] { "start", "save", "stop" }, this.service.Actions.Select(a => CarLinkJson.Parse(a)["action"].AsString()));
    }

    [Test]
    public void RunStartedOnTheCar_IsShownNotControlled()
    {
        this.service.StartFromPage("lab");
        this.TickUntil(() => this.run.State != null, "the first state");
        Assert.IsTrue(this.run.IsMapping);
        Assert.IsFalse(this.run.IsOwnRun);
        Assert.IsFalse(this.run.CanStart);
        Assert.IsFalse(this.run.CanSave);
        Assert.IsFalse(this.run.CanStop);
        Assert.AreEqual("Mapping lab, started on the car", CarWorld.RunText(this.run));

        this.run.Save();
        this.run.Stop();
        this.run.StartMapping("mine");
        this.run.Tick(this.Now);
        CollectionAssert.IsEmpty(this.service.Actions, "nothing sent");
    }

    [Test]
    public void BusyAndBadName_ReportedWithoutARun()
    {
        this.TickUntil(() => this.run.CanStart, "the first state");
        this.run.StartMapping("my room");
        Assert.AreEqual(CarMapRun.NameHint, this.run.Message);
        CollectionAssert.IsEmpty(this.service.Actions);

        lock (this.service)
        {
            this.service.IsBusy = true;
        }
        // The panel last saw an idle service; the car answers 409
        this.run.StartMapping("room_2");
        this.TickUntil(() => this.run.Step == CarMapRunStep.Idle, "the refusal");
        Assert.AreEqual("The car is running another map command.", this.run.Message);
        Assert.IsNull(this.run.OwnedMap);
    }

    [Test]
    public void FailedSave_ReportedAndRunKept()
    {
        this.TickUntil(() => this.run.CanStart, "the first state");
        this.run.StartMapping("room_3");
        this.TickUntil(() => this.run.CanSave, "the run");
        this.service.FailNextResult = "failed: disk full";
        this.run.Save();
        this.TickUntil(() => this.run.Step == CarMapRunStep.Idle && this.run.Message != null, "the failure");
        Assert.AreEqual("The car could not save the map.", CarWorld.RunText(this.run));
        Assert.IsTrue(this.run.IsOwnRun);
        Assert.IsNull(this.run.DownloadedMap);
    }

    [Test]
    public void ApiDropsMidRun_RunContinuesAndControlsReturn()
    {
        this.TickUntil(() => this.run.CanStart, "the first state");
        this.run.StartMapping("room_4");
        this.TickUntil(() => this.run.CanSave, "the run");

        lock (this.service)
        {
            this.service.IsDown = true;
        }
        this.TickUntil(() => !this.run.IsReachable, "the drop");
        Assert.IsTrue(this.run.IsOwnRun, "run kept");
        Assert.IsFalse(this.run.CanSave);
        Assert.AreEqual("No map service found on car link", CarWorld.RunText(this.run));

        lock (this.service)
        {
            this.service.IsDown = false;
        }
        this.TickUntil(() => this.run.IsReachable, "the return");
        Assert.IsTrue(this.run.CanSave);
        this.run.Save();
        this.TickUntil(() => this.run.DownloadedMap == "room_4", "the download");
    }

    [Test]
    public void FailedDownload_RetriesThenGivesUp()
    {
        this.TickUntil(() => this.run.CanStart, "the first state");
        this.run.StartMapping("room_5");
        this.TickUntil(() => this.run.CanSave, "the run");

        lock (this.service)
        {
            this.service.FailFileRequests = 1;
        }
        this.run.Save();
        this.TickUntil(() => this.run.IsRetrying, "the failed request");
        Assert.AreEqual("Download interrupted; retrying", CarWorld.RunText(this.run));
        this.skipped += CarMapRun.RetryInterval;
        this.TickUntil(() => this.run.DownloadedMap == "room_5", "the retried download");
        Assert.IsNull(this.run.Message);

        lock (this.service)
        {
            this.service.FailFileRequests = CarMapRun.MaxAttempts;
        }
        this.run.Save();
        // An attempt can fail in the frame it starts, so skip each wait as soon as one is seen
        this.TickUntil(() =>
        {
            if (this.run.IsRetrying)
            {
                this.skipped += CarMapRun.RetryInterval;
            }
            return this.run.Message != null;
        }, "giving up");
        Assert.AreEqual("The car could not send the map files (500). Save again to retry.", this.run.Message);
        Assert.AreEqual(CarMapRunStep.Idle, this.run.Step);
    }

    [Test]
    public void Names_DefaultsFoldersAndResults()
    {
        Assert.AreEqual("map_20261023_1430", CarMapRun.DefaultName(new DateTime(2026, 10, 23, 14, 30, 59)));
        Assert.IsTrue(CarApiClient.IsValidName(CarMapRun.DefaultName(DateTime.Now)));
        Assert.AreEqual("racecar-neo.local", CarMapRun.CarFolderName("racecar-neo.local"));
        Assert.AreEqual("10.42.0.1", CarMapRun.CarFolderName("10.42.0.1"));
        Assert.AreEqual("a_b_c", CarMapRun.CarFolderName("a/b\\c"));
        Assert.AreEqual("car", CarMapRun.CarFolderName(".."));
        Assert.AreEqual("car", CarMapRun.CarFolderName(null));
        Assert.IsFalse(CarMapRun.IsFailedResult(null));
        Assert.IsFalse(CarMapRun.IsFailedResult("ok"));
        Assert.IsFalse(CarMapRun.IsFailedResult("saved to room"));
        Assert.IsTrue(CarMapRun.IsFailedResult("failed: not mapping"));
        Assert.IsTrue(CarMapRun.IsFailedResult("error: timeout"));
        Assert.AreEqual("-", CarWorld.RunText(null));
    }

    [Test]
    public void Recorder_WritesRowsAtItsInterval()
    {
        using (SessionRecorder recorder = new SessionRecorder())
        {
            recorder.Begin(this.folder);
            TrajectorySample sample = new TrajectorySample
            {
                CarTime = 1234.5,
                Pose = new CarPose(1.25, -0.5, 0.75),
                PoseSource = CarPoseSource.Map,
                Speed = 0.42,
                CommandSpeed = 0.2,
                CommandSteer = -0.1,
                DriveSource = MuxSource.Gamepad,
                Localized = true,
            };
            recorder.Record(10.0, sample);
            recorder.Record(10.05, sample);
            sample.CarTime = double.NaN;
            sample.Speed = double.NaN;
            sample.Localized = null;
            recorder.Record(10.1, sample);
            Assert.AreEqual(2, recorder.RowCount);
            recorder.End();

            string[] lines = File.ReadAllLines(recorder.Path);
            Assert.AreEqual(SessionRecorder.Header, lines[0]);
            Assert.AreEqual("1234.500,1.2500,-0.5000,0.7500,map,0.420,0.200,-0.1000,gamepad,1", lines[1]);
            Assert.AreEqual(",1.2500,-0.5000,0.7500,map,,0.200,-0.1000,gamepad,", lines[2]);
        }
    }
}
