using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The car link parsers against frames recorded from a real rosbridge 2.7.1 (ROS 2 Jazzy) with
/// a publisher of known values for every topic the simulator reads, JSON and CBOR.
/// </summary>
public class CarLinkFixtureTests
{
    private static string Folder
    {
        get { return Path.Combine(Application.dataPath, "Tests", "Editor", "CarLinkFixtures"); }
    }

    /// <summary>
    /// The recorded publish operation's message, checked to be on the topic.
    /// </summary>
    private static CarLinkValue Message(string file, string topic)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(CarLinkFixtureTests.Folder, file));
        CarLinkValue op = file.EndsWith(".cbor", StringComparison.Ordinal)
            ? CarLinkCbor.Read(bytes)
            : CarLinkJson.Parse(Encoding.UTF8.GetString(bytes));
        Assert.AreEqual("publish", op["op"].AsString(), file);
        Assert.AreEqual(topic, op["topic"].AsString(), file);
        return op["msg"];
    }

    [TestCase("racecar_info.json")]
    [TestCase("racecar_info.cbor")]
    public void RacecarInfo(string file)
    {
        CarLinkValue message = CarLinkFixtureTests.Message(file, "/racecar/info");
        RacecarInfo info = global::RacecarInfo.Parse(CarLinkJson.Parse(message["data"].AsString()));
        Assert.IsTrue(info.IsCompatible);
        Assert.AreEqual(0.5, info.FirmwareMinSpeed);
        Assert.AreEqual(1080, info.LidarSamples);
        Assert.IsNull(info.Frames["imu_x_mm"]);
        Assert.IsNull(info.SpeedCap);
    }

    [TestCase("mux_state.json")]
    [TestCase("mux_state.cbor")]
    public void MuxState(string file)
    {
        CarLinkValue message = CarLinkFixtureTests.Message(file, "/mux/state");
        MuxState state = global::MuxState.Parse(CarLinkJson.Parse(message["data"].AsString()));
        Assert.AreEqual(MuxSource.Idle, state.Source);
        Assert.IsFalse(state.HasRc);
        Assert.IsNull(state.CapApplied);
    }

    [Test]
    public void AutonomyStatus_Json()
    {
        // rosbridge 2.7.1 drops this type under CBOR (byte field), so it is read as JSON only
        AutonomyStatus status = global::AutonomyStatus.Parse(CarLinkFixtureTests.Message("autonomy_status.json", "/autonomy/status"));
        Assert.AreEqual("mapping", status.Mode);
        Assert.AreEqual("lab", status.Map);
        Assert.IsTrue(status.Localized.Value);
        Assert.IsFalse(File.Exists(Path.Combine(CarLinkFixtureTests.Folder, "autonomy_status.cbor")));
    }

    [TestCase("autonomy_pose.json")]
    [TestCase("autonomy_pose.cbor")]
    public void Pose(string file)
    {
        PoseStamped2D pose = PoseStamped2D.Parse(CarLinkFixtureTests.Message(file, "/autonomy/pose"));
        Assert.AreEqual("map", pose.FrameId);
        Assert.AreEqual(1.25, pose.X, 1e-9);
        Assert.AreEqual(-0.5, pose.Y, 1e-9);
        Assert.AreEqual(0.75, pose.Yaw, 1e-9);
        Assert.AreEqual(36, pose.Covariance.Length);
        Assert.AreEqual(0.001, pose.Covariance[35], 1e-12);
        Assert.Greater(pose.Stamp, 1e9);
    }

    [TestCase("map.json")]
    [TestCase("map.cbor")]
    public void Grid(string file)
    {
        OccupancyGridMsg grid = OccupancyGridMsg.Parse(CarLinkFixtureTests.Message(file, "/map"));
        Assert.AreEqual(4, grid.Width);
        Assert.AreEqual(3, grid.Height);
        Assert.AreEqual(0.05f, grid.Resolution, 1e-7f);
        Assert.AreEqual(-1.0, grid.OriginX, 1e-9);
        Assert.AreEqual(new sbyte[] { -1, 0, 100, 0, 0, 65, -1, 100, 0, 0, 0, 0 }, grid.Data);
    }

    [TestCase("scan.json")]
    [TestCase("scan.cbor")]
    public void Scan(string file)
    {
        LaserScanMsg scan = LaserScanMsg.Parse(CarLinkFixtureTests.Message(file, "/scan"));
        Assert.AreEqual("laser", scan.FrameId);
        Assert.AreEqual(1080, scan.Ranges.Length);
        // Infinity: CBOR keeps it; rosbridge's JSON writes null, read as NaN
        Assert.IsTrue(float.IsInfinity(scan.Ranges[0]) || float.IsNaN(scan.Ranges[0]), file);
        Assert.IsFalse(scan.IsValid(0));
        Assert.AreEqual(1.001f, scan.Ranges[1], 1e-6f);
        Assert.AreEqual(2.079f, scan.Ranges[1079], 1e-5f);
        Assert.AreEqual(0.15f, scan.RangeMin, 1e-6f);
    }

    [Test]
    public void Scan_CborUsesTypedArrays()
    {
        CarLinkValue message = CarLinkFixtureTests.Message("scan.cbor", "/scan");
        Assert.AreEqual(CarLinkValueKind.Floats, message["ranges"].Kind);
        Assert.AreEqual(CarLinkValueKind.SBytes, CarLinkFixtureTests.Message("map.cbor", "/map")["data"].Kind);
    }

    [TestCase("odom_integrated.json")]
    [TestCase("odom_integrated.cbor")]
    public void Odometry(string file)
    {
        OdometryMsg odometry = OdometryMsg.Parse(CarLinkFixtureTests.Message(file, "/odom/integrated"));
        Assert.AreEqual("base_link", odometry.ChildFrameId);
        Assert.AreEqual(0.5, odometry.X, 1e-9);
        Assert.AreEqual(0.65, odometry.LinearX, 1e-9);
        Assert.AreEqual(-0.3, odometry.AngularZ, 1e-9);
    }

    [TestCase("imu_fused.json")]
    [TestCase("imu_fused.cbor")]
    public void Imu(string file)
    {
        ImuMsg imu = ImuMsg.Parse(CarLinkFixtureTests.Message(file, "/imu/fused"));
        Assert.AreEqual(0.5f, imu.AngularVelocity.z, 1e-6f);
        Assert.AreEqual(9.81f, imu.LinearAcceleration.z, 1e-5f);
    }

    [TestCase("encoder_speed.json")]
    [TestCase("encoder_speed.cbor")]
    public void Encoder(string file)
    {
        Assert.AreEqual(0.65f, Float32Msg.Parse(CarLinkFixtureTests.Message(file, "/encoder/speed")).Data, 1e-6f);
    }

    [TestCase("mux_out.json")]
    [TestCase("mux_out.cbor")]
    public void MuxOut(string file)
    {
        DriveCmd command = DriveCmd.Parse(CarLinkFixtureTests.Message(file, "/mux_out"));
        Assert.AreEqual("base_link", command.FrameId);
        Assert.AreEqual(0f, command.Speed);
    }

    [TestCase("tf_static.json")]
    [TestCase("tf_static.cbor")]
    public void TfStatic(string file)
    {
        CarLinkValue transform = CarLinkFixtureTests.Message(file, "/tf_static")["transforms"][0];
        Assert.AreEqual("laser", transform["child_frame_id"].AsString());
        Assert.AreEqual(0.2383, CarLinkFields.Double(transform, "transform.translation.x"), 1e-9);
        Assert.AreEqual(179.619, CarLinkFields.Yaw(transform, "transform.rotation") * 180 / Math.PI, 1e-6);
    }

    [Test]
    public void ServiceResponse()
    {
        CarLinkValue reply = CarLinkJson.Parse(File.ReadAllText(Path.Combine(CarLinkFixtureTests.Folder, "service_get_time.json")));
        Assert.AreEqual("service_response", reply["op"].AsString());
        Assert.IsTrue(reply["result"].AsBool());
        Assert.AreEqual("/rosapi/get_time", reply["service"].AsString(), "rosbridge escapes '/' as '\\/'");
        Assert.Greater(CarLinkFields.Stamp(reply["values"], "time"), 1e9);
    }

    [TestCase("advertise_refused.json")]
    [TestCase("service_refused.json")]
    public void Refusals_AreSilent(string file)
    {
        // rosbridge 2.7.1 answers neither a refused advertise nor a refused service call
        CarLinkValue recorded = CarLinkJson.Parse(File.ReadAllText(Path.Combine(CarLinkFixtureTests.Folder, file)));
        Assert.AreEqual("none", recorded["op"].AsString());
    }
}
