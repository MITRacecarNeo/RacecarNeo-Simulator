using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Car link targets, recent cars, the -racecarsim-car option, the pose tracker, and the car world
/// panel text.
/// </summary>
public class CarLinkWorldTests
{
    [TestCase("10.42.0.1", "10.42.0.1", 9090, 8084)]
    [TestCase("racecar-neo.local", "racecar-neo.local", 9090, 8084)]
    [TestCase(" 192.168.1.2 ", "192.168.1.2", 9090, 8084)]
    [TestCase("127.0.0.1:19090", "127.0.0.1", 19090, 8084)]
    [TestCase("127.0.0.1:19090:18084", "127.0.0.1", 19090, 18084)]
    public void Target_Parses(string text, string host, int bridgePort, int apiPort)
    {
        Assert.IsTrue(CarLinkTarget.TryParse(text, out CarLinkTarget target));
        Assert.AreEqual(host, target.Host);
        Assert.AreEqual(bridgePort, target.BridgePort);
        Assert.AreEqual(apiPort, target.ApiPort);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("my car")]
    [TestCase("http://10.42.0.1")]
    [TestCase("10.42.0.1:0")]
    [TestCase("10.42.0.1:70000")]
    [TestCase("10.42.0.1:abc")]
    [TestCase("10.42.0.1:1:2:3")]
    [TestCase(null)]
    public void Target_RejectsBadText(string text)
    {
        Assert.IsFalse(CarLinkTarget.TryParse(text, out CarLinkTarget _));
    }

    [Test]
    public void Target_LabelAndAddress()
    {
        Assert.AreEqual("10.42.0.1", new CarLinkTarget().Label);
        CarLinkTarget custom = new CarLinkTarget { Host = "127.0.0.1", BridgePort = 19090, ApiPort = 18084 };
        Assert.AreEqual("127.0.0.1:19090:18084", custom.Label);
        Assert.AreEqual("ws://127.0.0.1:19090/", custom.BridgeUri.ToString());
        Assert.IsTrue(custom.SameAddress(new CarLinkTarget { Host = "127.0.0.1", BridgePort = 19090, ApiPort = 18084, Name = "other" }));
        Assert.IsFalse(custom.SameAddress(new CarLinkTarget { Host = "127.0.0.1" }));
    }

    [Test]
    public void RecentCars_NewestFirstWithoutDuplicates()
    {
        SavedData data = SavedData.Default;
        for (int i = 0; i < SavedData.MaxRecentCars + 2; i++)
        {
            data.RememberCar(new CarLinkTarget { Host = $"10.0.0.{i}" });
        }
        data.RememberCar(new CarLinkTarget { Host = "10.0.0.3", Name = "neo-3" });
        Assert.AreEqual(SavedData.MaxRecentCars, data.RecentCars.Count);
        Assert.AreEqual("10.0.0.3", data.RecentCars[0].Host);
        Assert.AreEqual("neo-3", data.RecentCars[0].Name);
        Assert.AreEqual(1, data.RecentCars.FindAll(car => car.Host == "10.0.0.3").Count);
    }

    [Test]
    public void RecentCars_RoundTripAndOldSaves()
    {
        SavedData data = SavedData.Default;
        data.RememberCar(new CarLinkTarget { Host = "racecar-neo.local", BridgePort = 9091, Name = "neo" });
        SavedData loaded = SavedDataManager.FromJson(UnityEngine.JsonUtility.ToJson(data));
        Assert.AreEqual(1, loaded.RecentCars.Count);
        Assert.AreEqual(9091, loaded.RecentCars[0].BridgePort);
        Assert.AreEqual("neo", loaded.RecentCars[0].Name);

        // A save written before recent cars existed
        SavedData old = SavedDataManager.FromJson("{\"Version\": 3, \"BestTimes\": []}");
        Assert.IsNotNull(old.RecentCars);
        Assert.AreEqual(0, old.RecentCars.Count);
    }

    [Test]
    public void LaunchOption_Car()
    {
        Assert.AreEqual("127.0.0.1:19090", LaunchOptions.Parse(new[] { "RacecarSim", "-racecarsim-car", "127.0.0.1:19090" }).Car);
        Assert.IsNull(LaunchOptions.Parse(new[] { "RacecarSim" }).Car);
        Assert.IsNull(LaunchOptions.Parse(new[] { "RacecarSim", "-racecarsim-car" }).Car);
    }

    [Test]
    public void Tracker_PassesThroughSamples()
    {
        CarPoseTracker tracker = new CarPoseTracker();
        Assert.IsFalse(tracker.TrySample(0, out CarPose _), "nothing before a pose");
        for (int i = 0; i <= 10; i++)
        {
            tracker.AddOdometry(i * 0.02, i * 0.01, 0, 0);
        }
        // 1.5 intervals plus the margin (0.05 s) behind: at 0.2 s the shown pose is from 0.15 s
        Assert.IsTrue(tracker.TrySample(0.2, out CarPose pose));
        Assert.AreEqual(0.075, pose.X, 1e-9);
        Assert.AreEqual(CarPoseSource.Odometry, tracker.Source);
        Assert.IsTrue(tracker.TrySample(0.21, out pose));
        Assert.AreEqual(0.08, pose.X, 1e-9, "between samples");
    }

    [Test]
    public void Tracker_PrefersMapWhileAvailable()
    {
        CarPoseTracker tracker = new CarPoseTracker();
        tracker.AddOdometry(1.0, 5, 5, 0);
        tracker.AddMapPose(1.0, 1, 2, 0.5);
        Assert.IsTrue(tracker.TrySample(1.0, out CarPose pose));
        Assert.AreEqual(CarPoseSource.Odometry, tracker.Source, "map ignored while not available");
        tracker.IsMapAvailable = true;
        Assert.IsTrue(tracker.TrySample(1.0, out pose));
        Assert.AreEqual(CarPoseSource.Map, tracker.Source);
        Assert.AreEqual(1, pose.X);
        Assert.AreEqual(0.5, pose.Yaw);
    }

    [Test]
    public void Tracker_FallsBackAndGoesStale()
    {
        CarPoseTracker tracker = new CarPoseTracker { IsMapAvailable = true };
        tracker.AddMapPose(1.0, 1, 2, 0);
        tracker.AddOdometry(1.4, 7, 8, 0);
        Assert.IsTrue(tracker.TrySample(1.6, out CarPose pose));
        Assert.AreEqual(CarPoseSource.Odometry, tracker.Source, "map older than 0.5 s");
        Assert.AreEqual(7, pose.X);
        Assert.IsFalse(tracker.IsStale);

        Assert.IsTrue(tracker.TrySample(3.0, out pose), "holds the last pose");
        Assert.IsTrue(tracker.IsStale);
        Assert.AreEqual(7, pose.X);
    }

    [Test]
    public void SampleTime_UsesStampsOnceSynchronized()
    {
        LinkClock clock = new LinkClock();
        Assert.AreEqual(10.0, CarLinkSession.SampleTime(5000.0, 10.0, clock), "arrival before the clock syncs");
        // Car clock 4990 s ahead of link time, 4 ms round trips
        for (int i = 0; i < LinkClock.MinSamples; i++)
        {
            clock.AddSample(i, 4990 + i + 0.002, i + 0.004);
        }
        Assert.AreEqual(9.97, CarLinkSession.SampleTime(4999.97, 10.0, clock), 1e-6, "stamp through the clock");
        Assert.AreEqual(10.0, CarLinkSession.SampleTime(5000.05, 10.0, clock), 1e-9, "never after its arrival");
        Assert.AreEqual(10.0, CarLinkSession.SampleTime(4995.0, 10.0, clock), 1e-9, "far older than its arrival");
        Assert.AreEqual(10.0, CarLinkSession.SampleTime(0, 10.0, clock), "no stamp");
    }

    [Test]
    public void Tracker_SteadyMotionFromEvenStamps()
    {
        // A car at 1 m/s posting at 20 Hz; poses arrive in bursts up to 60 ms late. Timed by their
        // stamps, the shown car moves at a steady speed; timed by arrival, it speeds up and stops.
        System.Random random = new System.Random(3);
        CarPoseTracker byStamp = new CarPoseTracker { IsMapAvailable = true };
        CarPoseTracker byArrival = new CarPoseTracker { IsMapAvailable = true };
        List<(double arrival, double stamp)> poses = new List<(double arrival, double stamp)>();
        for (int i = 0; i < 100; i++)
        {
            double stamp = i * 0.05;
            poses.Add((stamp + 0.005 + random.NextDouble() * 0.06, stamp));
        }
        poses.Sort((a, b) => a.arrival.CompareTo(b.arrival));
        int next = 0;
        double worstStamp = 0;
        double worstArrival = 0;
        double lastStamp = double.NaN;
        double lastArrival = double.NaN;
        for (double now = 1.0; now < 4.5; now += 1 / 60.0)
        {
            while (next < poses.Count && poses[next].arrival <= now)
            {
                byStamp.AddMapPose(poses[next].stamp, poses[next].stamp, 0, 0, poses[next].arrival);
                byArrival.AddMapPose(poses[next].arrival, poses[next].stamp, 0, 0);
                next++;
            }
            byStamp.TrySample(now, out CarPose a);
            byArrival.TrySample(now, out CarPose b);
            if (!double.IsNaN(lastStamp))
            {
                worstStamp = Math.Max(worstStamp, Math.Abs((a.X - lastStamp) * 60 - 1));
                worstArrival = Math.Max(worstArrival, Math.Abs((b.X - lastArrival) * 60 - 1));
            }
            lastStamp = a.X;
            lastArrival = b.X;
        }
        Assert.Less(worstStamp, 0.06, "speed within 6% every frame, never held");
        Assert.Greater(worstArrival, 0.2, "arrival timing wobbles");
    }

    [Test]
    public void Minimap_ViewFitsThenFollowsTheCar()
    {
        // A 100 x 80 cell map: at zoom 1 the window is 100 cells, centered (80 rows sit 10 in)
        (float x0, float y0, float side) = CarMinimap.View(100, 80, 1, 90, 70);
        Assert.AreEqual(100, side);
        Assert.AreEqual(0, x0);
        Assert.AreEqual(-10, y0, "zoom 1 ignores the car");

        (x0, y0, side) = CarMinimap.View(100, 80, 4, 50, 40);
        Assert.AreEqual(25, side);
        Assert.AreEqual(37.5f, x0, 1e-4);
        Assert.AreEqual(27.5f, y0, 1e-4, "centered on the car");

        (x0, y0, side) = CarMinimap.View(100, 80, 4, 98, 2);
        Assert.AreEqual(75, x0, 1e-4, "kept inside the right edge");
        Assert.AreEqual(0, y0, 1e-4, "kept inside the bottom edge");

        OccupancyGridMsg map = new OccupancyGridMsg { Width = 100, Height = 80, Resolution = 0.05f, OriginX = -2.5, OriginY = -2.0 };
        Vector2 cell = CarMinimap.ToCell(map, 0, 0);
        Assert.AreEqual(50, cell.x, 1e-4);
        Assert.AreEqual(40, cell.y, 1e-4);
        Assert.AreNotEqual(CarMinimap.CellColor(0), CarMinimap.CellColor(100));
        Assert.AreEqual(CarMinimap.CellColor(-1), CarMinimap.CellColor(50), "between thresholds reads unknown");
    }

    [Test]
    public void Tracker_InterpolatesYawTheShortWay()
    {
        List<(double time, CarPose pose)> samples = new List<(double time, CarPose pose)>
        {
            (0, new CarPose(0, 0, Math.PI - 0.1)),
            (1, new CarPose(0, 0, -Math.PI + 0.1)),
        };
        CarPose middle = CarPoseTracker.Interpolate(samples, 0.5);
        Assert.AreEqual(Math.PI, Math.Abs(middle.Yaw), 1e-9);
    }

    [Test]
    public void Tracker_DropsOutOfOrderSamples()
    {
        CarPoseTracker tracker = new CarPoseTracker();
        tracker.AddOdometry(2.0, 1, 0, 0);
        tracker.AddOdometry(1.5, 99, 0, 0);
        Assert.IsTrue(tracker.TrySample(2.0, out CarPose pose));
        Assert.AreEqual(1, pose.X);
    }

    [Test]
    public void PanelText()
    {
        Assert.AreEqual("Connected", CarWorld.StatusText(CarLinkStatus.Connected));
        Assert.AreEqual("Reconnecting", CarWorld.StatusText(CarLinkStatus.Reconnecting));
        Assert.AreEqual("No map", CarWorld.ModeText(null));
        AutonomyStatus mapping = new AutonomyStatus { Mode = "mapping", Map = "room_05" };
        Assert.AreEqual("Mapping room_05", CarWorld.ModeText(mapping));
        AutonomyStatus lost = new AutonomyStatus { Mode = "localization", Map = "lab", Localized = false };
        Assert.AreEqual("lab, not localized", CarWorld.ModeText(lost));
        Assert.AreEqual("Waiting", CarWorld.PoseText(new CarPoseTracker()));
    }

    [Test]
    public void FailureText_NamesTheCause()
    {
        CarLinkTarget target = new CarLinkTarget { Host = "10.42.0.7" };
        StringAssert.Contains("Join the car's network", CarLinkSession.Describe(new CarApiException(CarApiFailure.NoRoute, "x"), target));
        StringAssert.Contains("car link is off", CarLinkSession.Describe(new CarApiException(CarApiFailure.Refused, "x"), target));
        StringAssert.Contains("No answer from 10.42.0.7", CarLinkSession.Describe(new CarApiException(CarApiFailure.Timeout, "x"), target));
        StringAssert.Contains("error (500)", CarLinkSession.Describe(new CarApiException(CarApiFailure.Http, "x", 500), target));
    }
}
