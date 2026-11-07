using NUnit.Framework;

/// <summary>
/// Car link message parsing against samples in the car's interface format (link interface 1.0).
/// </summary>
public class CarLinkMessageTests
{
    private const string infoJson = @"{
  ""link_interface"": ""1.0"",
  ""driver_version"": ""0.9.1"",
  ""hostname"": ""racecar-neo-07"",
  ""services"": {""autonomy"": true, ""mapview"": true, ""simlink"": true},
  ""ports"": {""bridge"": 9090, ""api"": 8084},
  ""firmware"": {""source"": ""config"", ""min_speed_mps"": 0.5, ""max_speed_mps"": 4.0, ""slow_speed_mps"": 1.0, ""deadband"": 0.01, ""setpoint_offset_mps"": 0.175},
  ""lidar"": {""model"": ""RPLIDAR A1"", ""samples"": 1080, ""angle_min"": -3.14159, ""angle_max"": 3.14159, ""range_min"": 0.15, ""range_max"": 12.0, ""rate_hz"": 7.2},
  ""frames"": {""laser_x_mm"": 238.3, ""laser_y_mm"": 0.0, ""laser_z_mm"": 195.0, ""laser_yaw_deg"": 179.619, ""imu_x_mm"": null},
  ""calibration"": {""steady_speed_mps"": {""0.25"": 0.721, ""0.5"": 0.833, ""1.0"": 1.07}},
  ""caps"": {""mapping_speed_cap"": 0.02, ""sim_speed_cap"": 0.2, ""speed_cap"": null},
  ""twin"": {""available"": true}
}";

    [Test]
    public void RacecarInfo_ParsesContractSample()
    {
        RacecarInfo info = RacecarInfo.Parse(CarLinkJson.Parse(CarLinkMessageTests.infoJson));
        Assert.AreEqual(1, info.Major);
        Assert.AreEqual(0, info.Minor);
        Assert.IsTrue(info.IsCompatible);
        Assert.AreEqual("0.9.1", info.DriverVersion);
        Assert.AreEqual(9090, info.BridgePort);
        Assert.AreEqual(0.5, info.FirmwareMinSpeed);
        Assert.AreEqual(1080, info.LidarSamples);
        Assert.AreEqual(179.619, info.Frame("laser_yaw_deg", 180), 1e-9);
        Assert.IsNull(info.Frames["imu_x_mm"]);
        Assert.AreEqual(190, info.Frame("imu_x_mm", 190));
        Assert.AreEqual(0.833, info.Calibration.At("steady_speed_mps")["0.5"].AsDouble(), 1e-9);
        Assert.AreEqual(0.2, info.SimSpeedCap);
        Assert.IsNull(info.SpeedCap);
        Assert.IsTrue(info.TwinAvailable);
    }

    [Test]
    public void RacecarInfo_OtherMajorIsIncompatible()
    {
        RacecarInfo info = RacecarInfo.Parse(CarLinkJson.Parse("{\"link_interface\": \"2.3\"}"));
        Assert.AreEqual(2, info.Major);
        Assert.AreEqual(3, info.Minor);
        Assert.IsFalse(info.IsCompatible);
    }

    [Test]
    public void RacecarInfo_MissingVersionNamesTheField()
    {
        CarLinkFormatException exception = Assert.Throws<CarLinkFormatException>(
            () => RacecarInfo.Parse(CarLinkJson.Parse("{\"driver_version\": \"0.9.1\"}")));
        StringAssert.Contains("link_interface", exception.Message);
    }

    [TestCase(0.0, 0.0)]
    [TestCase(0.01, 0.0)]
    [TestCase(1.0, 4.0)]
    [TestCase(-1.0, -4.0)]
    [TestCase(0.2, 1.1717)]
    [TestCase(0.15, 0.9949)]
    public void RacecarInfo_SetpointFollowsFirmwareMap(double command, double expected)
    {
        RacecarInfo info = RacecarInfo.Parse(CarLinkJson.Parse(CarLinkMessageTests.infoJson));
        Assert.AreEqual(expected, info.SetpointFor(command).Value, 1e-3);
    }

    [Test]
    public void MuxState_ParsesContractSample()
    {
        MuxState state = MuxState.Parse(CarLinkJson.Parse(
            "{\"source\": \"sim\", \"twin\": true, \"sim_publishers\": 1, \"rb\": true, \"lb\": false, \"rb_wait_release\": false,"
            + " \"guard\": {\"active\": true, \"range_m\": 0.42}, \"cap\": {\"sim\": 0.2, \"speed\": null, \"mapping\": null, \"applied\": 0.2},"
            + " \"rc\": null, \"stamp\": 1792466298.12}"));
        Assert.AreEqual(MuxSource.Sim, state.Source);
        Assert.IsTrue(state.Twin);
        Assert.AreEqual(1, state.SimPublishers);
        Assert.IsTrue(state.Rb);
        Assert.IsTrue(state.GuardActive);
        Assert.AreEqual(0.42, state.GuardRange.Value, 1e-9);
        Assert.IsNull(state.CapSpeed);
        Assert.AreEqual(0.2, state.CapApplied);
        Assert.IsFalse(state.HasRc);
        Assert.AreEqual(1792466298.12, state.Stamp, 1e-6);
    }

    [Test]
    public void MuxState_UnknownSourceIsKept()
    {
        Assert.AreEqual(MuxSource.Unknown, MuxState.Parse(CarLinkJson.Parse("{\"source\": \"nav\"}")).Source);
        Assert.AreEqual(MuxSource.Idle, MuxState.Parse(CarLinkJson.Parse("{\"source\": \"idle\"}")).Source);
    }

    [Test]
    public void AutonomyStatus_ReadsKeyValues()
    {
        AutonomyStatus status = AutonomyStatus.Parse(CarLinkJson.Parse(
            "{\"level\": 0, \"name\": \"autonomy\", \"message\": \"ok\", \"hardware_id\": \"\","
            + " \"values\": [{\"key\": \"mode\", \"value\": \"mapping\"}, {\"key\": \"map\", \"value\": \"room_05\"},"
            + " {\"key\": \"localized\", \"value\": \"true\"}]}"));
        Assert.AreEqual("mapping", status.Mode);
        Assert.AreEqual("room_05", status.Map);
        Assert.IsTrue(status.Localized.Value);
    }

    [Test]
    public void Pose_ReadsYawAndCovariance()
    {
        // Yaw 90 degrees: z = w = sqrt(0.5)
        PoseStamped2D pose = PoseStamped2D.Parse(CarLinkJson.Parse(
            "{\"header\": {\"stamp\": {\"sec\": 100, \"nanosec\": 500000000}, \"frame_id\": \"map\"},"
            + " \"pose\": {\"pose\": {\"position\": {\"x\": 1.5, \"y\": -2.0, \"z\": 0.0},"
            + " \"orientation\": {\"x\": 0.0, \"y\": 0.0, \"z\": 0.7071067811865476, \"w\": 0.7071067811865476}},"
            + " \"covariance\": [0.01, 0, 0, 0, 0, 0, 0, 0.02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.03]}}"));
        Assert.AreEqual(100.5, pose.Stamp, 1e-9);
        Assert.AreEqual("map", pose.FrameId);
        Assert.AreEqual(1.5, pose.X);
        Assert.AreEqual(-2.0, pose.Y);
        Assert.AreEqual(System.Math.PI / 2, pose.Yaw, 1e-9);
        Assert.AreEqual(36, pose.Covariance.Length);
        Assert.AreEqual(0.03, pose.Covariance[35], 1e-12);
    }

    [Test]
    public void Pose_MissingPositionNamesThePath()
    {
        CarLinkFormatException exception = Assert.Throws<CarLinkFormatException>(() => PoseStamped2D.Parse(CarLinkJson.Parse(
            "{\"header\": {\"stamp\": {\"sec\": 1, \"nanosec\": 0}}, \"pose\": {\"pose\": {\"orientation\": {\"x\": 0, \"y\": 0, \"z\": 0, \"w\": 1}}}}")));
        StringAssert.Contains("pose.pose.position.x", exception.Message);
    }

    private const string gridJson = "{\"header\": {\"stamp\": {\"sec\": 5, \"nanosec\": 0}, \"frame_id\": \"map\"},"
        + " \"info\": {\"map_load_time\": {\"sec\": 0, \"nanosec\": 0}, \"resolution\": 0.05, \"width\": 3, \"height\": 2,"
        + " \"origin\": {\"position\": {\"x\": -1.0, \"y\": -0.5, \"z\": 0.0}, \"orientation\": {\"x\": 0, \"y\": 0, \"z\": 0, \"w\": 1}}},"
        + " \"data\": [-1, 0, 100, 0, 0, 65]}";

    [Test]
    public void Grid_FromJson()
    {
        OccupancyGridMsg grid = OccupancyGridMsg.Parse(CarLinkJson.Parse(CarLinkMessageTests.gridJson));
        Assert.AreEqual(0.05f, grid.Resolution);
        Assert.AreEqual(3, grid.Width);
        Assert.AreEqual(2, grid.Height);
        Assert.AreEqual(-1.0, grid.OriginX);
        Assert.AreEqual(-1, grid.At(0, 0));
        Assert.AreEqual(100, grid.At(2, 0));
        Assert.AreEqual(65, grid.At(2, 1));
        Assert.AreEqual(-1, grid.At(3, 0));
    }

    [Test]
    public void Grid_FromCborTypedArray()
    {
        // The same grid, data as a CBOR int8 typed array inside the JSON-built map
        CarLinkValue value = CarLinkJson.Parse(CarLinkMessageTests.gridJson);
        System.Collections.Generic.Dictionary<string, CarLinkValue> map = new System.Collections.Generic.Dictionary<string, CarLinkValue>();
        foreach (System.Collections.Generic.KeyValuePair<string, CarLinkValue> entry in value.Entries)
        {
            map[entry.Key] = entry.Value;
        }
        map["data"] = CarLinkCbor.Read(new byte[] { 0xd8, 0x48, 0x46, 0xff, 0x00, 0x64, 0x00, 0x00, 0x41 });
        OccupancyGridMsg grid = OccupancyGridMsg.Parse(CarLinkValue.FromMap(map));
        Assert.AreEqual(65, grid.At(2, 1));
    }

    [Test]
    public void Grid_SizeMismatchThrows()
    {
        Assert.Throws<CarLinkFormatException>(
            () => OccupancyGridMsg.Parse(CarLinkJson.Parse(CarLinkMessageTests.gridJson.Replace("\"width\": 3", "\"width\": 4"))));
    }

    [Test]
    public void Scan_ReadsRangesWithInfinity()
    {
        LaserScanMsg scan = LaserScanMsg.Parse(CarLinkJson.Parse(
            "{\"header\": {\"stamp\": {\"sec\": 7, \"nanosec\": 250000000}, \"frame_id\": \"laser\"},"
            + " \"angle_min\": -3.14159, \"angle_max\": 3.14159, \"angle_increment\": 0.005823, \"time_increment\": 0.0001,"
            + " \"scan_time\": 0.135, \"range_min\": 0.15, \"range_max\": 12.0, \"ranges\": [1.0, Infinity, 0.1, 13.0], \"intensities\": []}"));
        Assert.AreEqual(7.25, scan.Stamp, 1e-9);
        Assert.AreEqual(4, scan.Ranges.Length);
        Assert.IsTrue(scan.IsValid(0));
        Assert.IsFalse(scan.IsValid(1));
        Assert.IsFalse(scan.IsValid(2));
        Assert.IsFalse(scan.IsValid(3));
    }

    [Test]
    public void Odometry_ReadsPoseAndTwist()
    {
        OdometryMsg odometry = OdometryMsg.Parse(CarLinkJson.Parse(
            "{\"header\": {\"stamp\": {\"sec\": 3, \"nanosec\": 0}, \"frame_id\": \"odom\"}, \"child_frame_id\": \"base_link\","
            + " \"pose\": {\"pose\": {\"position\": {\"x\": 0.5, \"y\": 0.25, \"z\": 0}, \"orientation\": {\"x\": 0, \"y\": 0, \"z\": 0, \"w\": 1}}},"
            + " \"twist\": {\"twist\": {\"linear\": {\"x\": 0.65, \"y\": 0, \"z\": 0}, \"angular\": {\"x\": 0, \"y\": 0, \"z\": -0.3}}}}"));
        Assert.AreEqual("base_link", odometry.ChildFrameId);
        Assert.AreEqual(0.65, odometry.LinearX, 1e-9);
        Assert.AreEqual(-0.3, odometry.AngularZ, 1e-9);
        Assert.AreEqual(0, odometry.Yaw, 1e-12);
    }

    [Test]
    public void Imu_ReadsVectors()
    {
        ImuMsg imu = ImuMsg.Parse(CarLinkJson.Parse(
            "{\"header\": {\"stamp\": {\"sec\": 1, \"nanosec\": 0}, \"frame_id\": \"imu_link\"},"
            + " \"angular_velocity\": {\"x\": 0.0, \"y\": 0.0, \"z\": 0.5}, \"linear_acceleration\": {\"x\": 0.1, \"y\": 0.0, \"z\": 9.81}}"));
        Assert.AreEqual(0.5f, imu.AngularVelocity.z, 1e-6f);
        Assert.AreEqual(9.81f, imu.LinearAcceleration.z, 1e-5f);
    }

    [Test]
    public void DriveCmd_FromStudentMatchesLibrary()
    {
        // The physical car's library sends speed times max speed and negates the angle
        DriveCmd command = DriveCmd.FromStudent(0.8f, 0.5f, 0.25f, 12.75);
        Assert.AreEqual(0.2f, command.Speed, 1e-6f);
        Assert.AreEqual(-0.5f, command.SteeringAngle, 1e-6f);
        Assert.AreEqual(1f, DriveCmd.FromStudent(2f, 0f, 1f, 0).Speed);
    }

    [Test]
    public void DriveCmd_RoundTripsThroughJson()
    {
        DriveCmd command = DriveCmd.FromStudent(-0.4f, -1f, 0.5f, 1792466298.125);
        CarLinkValue value = CarLinkJson.Parse(CarLinkJson.Write(command.ToValue()));
        Assert.AreEqual(1792466298L, value.At("header.stamp.sec").AsLong());
        Assert.AreEqual(125000000L, value.At("header.stamp.nanosec").AsLong());
        Assert.AreEqual("base_link", value.At("header.frame_id").AsString());
        DriveCmd parsed = DriveCmd.Parse(value);
        Assert.AreEqual(-0.2f, parsed.Speed, 1e-6f);
        Assert.AreEqual(1f, parsed.SteeringAngle, 1e-6f);
        Assert.AreEqual(1792466298.125, parsed.Stamp, 1e-6);
    }

    [Test]
    public void ApiState_ToleratesWaitingNulls()
    {
        ApiState waiting = ApiState.Parse(CarLinkJson.Parse(
            "{\"mode\": null, \"map\": null, \"localized\": null, \"pose\": null, \"job\": {\"running\": false}, \"twin\": false, \"link_clients\": 1}"));
        Assert.IsTrue(waiting.IsWaiting);
        Assert.IsFalse(waiting.HasPose);
        ApiState state = ApiState.Parse(CarLinkJson.Parse(
            "{\"mode\": \"localization\", \"map\": \"p6_room\", \"localized\": true, \"pose\": [0.07, 0.5, 1.3],"
            + " \"job\": {\"running\": true, \"action\": \"load\", \"client\": \"racecarsim\"}, \"twin\": true, \"link_clients\": 2}"));
        Assert.IsFalse(state.IsWaiting);
        Assert.AreEqual(1.3, state.PoseYaw, 1e-9);
        Assert.AreEqual("racecarsim", state.JobClient);
        Assert.AreEqual(2, state.LinkClients);
    }

    [Test]
    public void MapEntry_ParsesList()
    {
        System.Collections.Generic.List<MapEntry> maps = MapEntry.ParseList(CarLinkJson.Parse(
            "{\"maps\": [{\"name\": \"room_05\", \"saved\": true, \"autosave\": false, \"created\": \"2026-10-08\","
            + " \"size_m\": [9.25, 7.65], \"resolution\": 0.05, \"driver_version\": \"0.9.1\", \"session\": true, \"rgbd\": false}]}"));
        Assert.AreEqual(1, maps.Count);
        Assert.AreEqual("room_05", maps[0].Name);
        Assert.AreEqual(9.25, maps[0].Width);
        Assert.IsTrue(maps[0].HasSession);
    }
}
