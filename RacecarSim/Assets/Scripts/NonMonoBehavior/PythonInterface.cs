using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// Manages UDP communication with one or more Python scripts.
/// </summary>
/// <remarks>
/// The sync client (unityPort) runs on the main thread in lockstep with Unity frames. The async
/// client (unityPortAsync) runs on a background thread for connect, exit, and Jupyter sensor
/// calls. Connect and exit requests are queued by the background thread and applied on the main
/// thread in ProcessPendingRequests, so the endpoint list is only touched by the main thread.
/// </remarks>
public class PythonInterface
{
    #region Constants
    /// <summary>
    /// The current version of the protocol used to communicate with racecar_core.
    /// </summary>
    /// <remarks>
    /// When the communication protocol between RacecarSim and racecar_core are changed, this version number
    /// should be incremented both here and in racecar_core_sim.py. This allows us to immediately detect
    /// if a user attempts to use incompatible versions of RacecarSim and racecar_core.
    /// </remarks>
    private const int version = 1;

    /// <summary>
    /// The UDP port used by the Unity simulation (this program).
    /// </summary>
    private const int unityPort = 5065;

    /// <summary>
    /// The UDP port used by the Unity simulation (this program) for async (Jupyter Notebook) calls.
    /// </summary>
    private const int unityPortAsync = 5064;

    /// <summary>
    /// IPAddress.Any (0.0.0.0) accepts connections from any interface,
    /// which is required for WSL 2 (NAT mode) where Python traffic arrives
    /// on the virtual ethernet adapter rather than the loopback interface.
    /// PythonSourceFilter drops packets from other hosts.
    /// </summary>
    private static readonly IPAddress bindAddress = IPAddress.Any;

    /// <summary>
    /// The time (in ms) to wait for Python to respond.
    /// </summary>
    private const int timeoutTime = 5000;

    /// <summary>
    /// The time (in ms) the async thread waits for a packet before checking whether it should stop.
    /// </summary>
    private const int asyncPollTime = 500;

    /// <summary>
    /// The number of packets used to send a color image.
    /// </summary>
    private const int colorImagePackets = 32;

    /// <summary>
    /// Windows IOCTL SIO_UDP_CONNRESET. Disabling it stops an ICMP "port unreachable" from a closed
    /// Python program from surfacing as an exception on the next receive, which may belong to a
    /// different program.
    /// </summary>
    private const int sioUdpConnReset = -1744830452;
    #endregion

    #region Public
    /// <summary>
    /// An array in which each entry indicates whether the racecar of the same index is connected to a Python script.
    /// </summary>
    public bool[] ConnectedPrograms
    {
        get
        {
            return this.pythonEndPoints.Select(x => x != null).ToArray();
        }
    }

    /// <summary>
    /// A message describing why the UDP sockets could not be opened, or null if they opened.
    /// </summary>
    public string StartupError { get; private set; }

    public PythonInterface()
    {
        this.wasExitHandled = false;
        this.pythonEndPoints = new List<IPEndPoint>();

        try
        {
            this.udpClient = PythonInterface.CreateClient(PythonInterface.unityPort, PythonInterface.timeoutTime);
            this.udpClientAsync = PythonInterface.CreateClient(PythonInterface.unityPortAsync, PythonInterface.asyncPollTime);
        }
        catch (SocketException e)
        {
            this.udpClient?.Close();
            this.udpClient = null;
            this.udpClientAsync = null;
            this.StartupError = e.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"UDP port {PythonInterface.unityPort} or {PythonInterface.unityPortAsync} is already in use, so Python programs cannot connect. Close any other copy of RacecarSim and restart."
                : $"Unable to open UDP ports {PythonInterface.unityPort} and {PythonInterface.unityPortAsync}, so Python programs cannot connect. Error: {e.SocketErrorCode}.";
            Debug.LogError(this.StartupError);
            return;
        }

        // Create a new thread for handling async calls
        this.asyncClientThread = new Thread(new ThreadStart(this.ProcessAsyncCalls))
        {
            IsBackground = true
        };
        this.asyncClientThread.Start();
    }

    /// <summary>
    /// Closes all UDP clients and sends an exit command to each connected Python script.
    /// </summary>
    public void HandleExit()
    {
        if (this.wasExitHandled)
        {
            return;
        }

        this.isClosing = true;
        foreach (IPEndPoint endpoint in this.pythonEndPoints)
        {
            if (endpoint != null)
            {
                this.TrySend(this.udpClient, new byte[] { (byte)Header.unity_exit }, endpoint);
            }
        }
        this.pythonEndPoints.Clear();
        LevelManager.UpdateConnectedPrograms();

        this.udpClient?.Close();
        this.udpClientAsync?.Close();
        this.wasExitHandled = true;
    }

    /// <summary>
    /// Tells Python to run the user's start function.
    /// </summary>
    public void HandleStart()
    {
        this.PythonCall(Header.unity_start);
    }

    /// <summary>
    /// Tells Python to run the user's update function.
    /// </summary>
    public void HandleUpdate()
    {
        // Includes the wait for each program's reply
        using SimProfiler.Scope profile = SimProfiler.Measure(SimProfiler.Section.PythonSync);
        this.PythonCall(Header.unity_update);
    }

    /// <summary>
    /// Applies connect and exit requests received by the async thread. Call once per frame on the main thread.
    /// </summary>
    public void ProcessPendingRequests()
    {
        while (this.pendingMessages.TryDequeue(out string message))
        {
            LevelManager.ShowMessage(message, Color.red, 5.0f);
        }

        while (this.pendingRequests.TryDequeue(out PendingRequest request))
        {
            if (this.wasExitHandled)
            {
                continue;
            }

            if (request.Header == Header.connect)
            {
                this.HandleConnect(request);
            }
            else
            {
                this.RemoveSyncClient(request.EndPoint);
            }
        }
    }

    /// <summary>
    /// Returns whether a request packet from Python is long enough for its header.
    /// </summary>
    /// <param name="data">The packet, starting with the header byte.</param>
    /// <returns>True if the packet has a known header and every byte that header reads.</returns>
    public static bool IsWellFormed(byte[] data)
    {
        if (data == null || data.Length == 0 || !Enum.IsDefined(typeof(Header), (int)data[0]))
        {
            return false;
        }
        return data.Length >= PythonInterface.RequiredLength((Header)data[0]);
    }
    #endregion

    /// <summary>
    /// Header bytes used in the communication protocol.
    /// </summary>
    private enum Header
    {
        error,
        connect,
        unity_start,
        unity_update,
        unity_exit,
        python_finished,
        python_send_next,
        python_exit,
        racecar_go,
        racecar_set_start_update,
        racecar_get_delta_time,
        racecar_set_update_slow_time,
        camera_get_color_image,
        camera_get_depth_image,
        camera_get_width,
        camera_get_height,
        controller_is_down,
        controller_was_pressed,
        controller_was_released,
        controller_get_trigger,
        controller_get_joystick,
        display_show_image,
        drive_set_speed_angle,
        drive_stop,
        drive_set_max_speed,
        lidar_get_num_samples,
        lidar_get_samples,
        physics_get_linear_acceleration,
        physics_get_angular_velocity,
    }

    /// <summary>
    /// The error codes used in the communication protocol.
    /// </summary>
    private enum Error
    {
        generic,
        timeout,
        python_exception,
        no_free_car,
        python_outdated,
        racecarsim_outdated,
        fragment_mismatch
    }

    /// <summary>
    /// A connect or exit request received on the async thread, applied on the main thread.
    /// </summary>
    private struct PendingRequest
    {
        public Header Header;
        public IPEndPoint EndPoint;
        public int PythonVersion;
    }

    /// <summary>
    /// The minimum packet length, in bytes, for a request with the given header.
    /// </summary>
    private static int RequiredLength(Header header)
    {
        switch (header)
        {
            case Header.error:
            case Header.controller_is_down:
            case Header.controller_was_pressed:
            case Header.controller_was_released:
            case Header.controller_get_trigger:
            case Header.controller_get_joystick:
                return 2;
            case Header.drive_set_speed_angle:
                return 12;
            case Header.drive_set_max_speed:
                return 8;
            default:
                return 1;
        }
    }

    /// <summary>
    /// Creates a UDP client bound to a port on bindAddress.
    /// </summary>
    private static UdpClient CreateClient(int port, int receiveTimeout)
    {
        UdpClient client = new UdpClient(new IPEndPoint(PythonInterface.bindAddress, port));
        client.Client.ReceiveTimeout = receiveTimeout;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            client.Client.IOControl(PythonInterface.sioUdpConnReset, new byte[] { 0, 0, 0, 0 }, null);
        }
        catch (Exception e) when (e is SocketException || e is PlatformNotSupportedException || e is NotSupportedException)
        {
            Debug.LogWarning($"Unable to disable UDP connection reset reporting: {e.Message}");
        }
#endif
        return client;
    }

    /// <summary>
    /// True if exit was properly handled.
    /// </summary>
    private bool wasExitHandled;

    /// <summary>
    /// Set before the sockets close so the async thread stops instead of reporting errors.
    /// </summary>
    private volatile bool isClosing;

    /// <summary>
    /// Connect and exit requests waiting for the main thread.
    /// </summary>
    private readonly ConcurrentQueue<PendingRequest> pendingRequests = new ConcurrentQueue<PendingRequest>();

    /// <summary>
    /// Messages from the async thread waiting to be shown on screen by the main thread.
    /// </summary>
    private readonly ConcurrentQueue<string> pendingMessages = new ConcurrentQueue<string>();

    /// <summary>
    /// Decides which source addresses may connect.
    /// </summary>
    private readonly PythonSourceFilter sourceFilter = new PythonSourceFilter();

    /// <summary>
    /// Source addresses already reported as rejected, so each is shown once.
    /// </summary>
    private readonly HashSet<IPAddress> reportedRejected = new HashSet<IPAddress>();

    /// <summary>
    /// Headers already reported as unsupported, so each is logged once.
    /// </summary>
    private readonly HashSet<Header> reportedUnsupported = new HashSet<Header>();

    /// <summary>
    /// Sends a packet, logging instead of throwing if the socket is closed or the send fails.
    /// </summary>
    private void TrySend(UdpClient client, byte[] data, IPEndPoint endPoint)
    {
        if (client == null || endPoint == null)
        {
            return;
        }

        try
        {
            client.Send(data, data.Length, endPoint);
        }
        catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
        {
            if (!this.isClosing)
            {
                Debug.LogError($"Unable to send to Python at [{endPoint}]. Error: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Logs an unsupported request the first time its header is seen. Called from both threads.
    /// </summary>
    private void ReportUnsupportedOnce(Header header, string reason)
    {
        lock (this.reportedUnsupported)
        {
            if (!this.reportedUnsupported.Add(header))
            {
                return;
            }
        }
        Debug.LogError($">> Error: The function {header} {reason}.");
    }

    /// <summary>
    /// Builds a two-byte error packet.
    /// </summary>
    private static byte[] ErrorPacket(Error errorCode)
    {
        return new byte[] { (byte)Header.error, (byte)errorCode };
    }

    #region Sync
    /// <summary>
    /// The UDP client used to send packets to Python.
    /// </summary>
    private readonly UdpClient udpClient;

    /// <summary>
    /// The UDP endpoints of the Python scripts(s) currently connected to RacecarSim.
    /// </summary>
    private readonly List<IPEndPoint> pythonEndPoints;

    /// <summary>
    /// Validates the protocol version of a connect request, pairs the script with a car, and replies.
    /// </summary>
    private void HandleConnect(PendingRequest request)
    {
        byte[] reply;
        if (PythonInterface.version == request.PythonVersion)
        {
            int? index = this.ConnectSyncClient(request.EndPoint);
            if (index.HasValue)
            {
                reply = new byte[] { (byte)Header.connect, (byte)index.Value };
            }
            else
            {
                this.ShowConnectError($"A Python program on port {request.EndPoint.Port} tried to connect, but every car already has a connected program.");
                reply = PythonInterface.ErrorPacket(Error.no_free_car);
            }
        }
        else if (PythonInterface.version > request.PythonVersion)
        {
            this.ShowConnectError("A Python program uses an outdated, incompatible version of racecar_core. Update the Python racecar libraries to the newest version.");
            reply = PythonInterface.ErrorPacket(Error.python_outdated);
        }
        else
        {
            this.ShowConnectError("A Python program uses a newer, incompatible version of racecar_core. Download the newest version of RacecarSim.");
            reply = PythonInterface.ErrorPacket(Error.racecarsim_outdated);
        }
        this.TrySend(this.udpClientAsync, reply, request.EndPoint);
    }

    /// <summary>
    /// Logs a connection problem and shows it on screen.
    /// </summary>
    private void ShowConnectError(string message)
    {
        Debug.LogError(message);
        LevelManager.ShowMessage(message, Color.red, 5.0f);
    }

    /// <summary>
    /// Connect the sync client to a Python script.
    /// </summary>
    /// <param name="remoteEndPoint">The endpoint of the Python script (IP and port).</param>
    /// <returns>The index of the car with which the script is paired, or null if the script could not be paired.</returns>
    private int? ConnectSyncClient(IPEndPoint remoteEndPoint)
    {
        IPEndPoint endPoint = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);

        // A program reconnecting from the same endpoint keeps its car
        int existing = this.pythonEndPoints.FindIndex(x => endPoint.Equals(x));
        if (existing >= 0)
        {
            return existing;
        }

        int index = -1;

        // Replace the first null end point, if any exist
        for (int i = 0; i < this.pythonEndPoints.Count; i++)
        {
            if (this.pythonEndPoints[i] == null)
            {
                this.pythonEndPoints[i] = endPoint;
                index = i;
                break;
            }
        }

        // Otherwise, add the end point to the end of the list
        if (index == -1)
        {
            if (this.pythonEndPoints.Count < LevelManager.NumPlayers)
            {
                index = this.pythonEndPoints.Count;
                this.pythonEndPoints.Add(endPoint);
            }
            else
            {
                // Every race car is already connected
                return null;
            }
        }

        LevelManager.UpdateConnectedPrograms();
        return index;
    }

    /// <summary>
    /// Disconnects a Python script from the sync client.
    /// </summary>
    /// <param name="pythonEndPoint">The endpoint of the Python script to remove.</param>
    private void RemoveSyncClient(IPEndPoint pythonEndPoint)
    {
        int index = this.pythonEndPoints.FindIndex(x => pythonEndPoint.Equals(x));
        if (index >= 0)
        {
            this.RemoveSyncClientAt(index);
        }
    }

    /// <summary>
    /// Disconnects the Python script paired with a car.
    /// </summary>
    /// <param name="index">The index of the car.</param>
    private void RemoveSyncClientAt(int index)
    {
        // Set the endpoint to null rather than removing it from the list to maintain
        // the mapping of remaining endpoints to cars
        this.pythonEndPoints[index] = null;
        LevelManager.GetCar(index)?.Drive.Stop();

        // We can safely remove any trailing null endpoints at the end of the list
        for (int i = this.pythonEndPoints.Count - 1; i >= 0 && this.pythonEndPoints[i] == null; i--)
        {
            this.pythonEndPoints.RemoveAt(i);
        }

        LevelManager.UpdateConnectedPrograms();
    }

    /// <summary>
    /// Calls a Python function on all connected scripts.
    /// </summary>
    /// <param name="function">The Python function to call (start or update)</param>
    private void PythonCall(Header function)
    {
        if (this.udpClient == null)
        {
            return;
        }

        for (int i = 0; i < this.pythonEndPoints.Count; i++)
        {
            if (this.pythonEndPoints[i] == null)
            {
                continue;
            }

            Racecar racecar = LevelManager.GetCar(i);
            IPEndPoint endPoint = this.pythonEndPoints[i];

            // Tell Python what function to call
            this.TrySend(this.udpClient, new byte[] { (byte)function }, endPoint);

            // Respond to API calls from Python until we receive a python_finished message
            bool pythonFinished = false;
            while (!pythonFinished)
            {
                // Receive a response from Python
                byte[] data = this.SafeReceive(i);
                if (data == null)
                {
                    break;
                }

                if (!PythonInterface.IsWellFormed(data))
                {
                    this.RejectRequest(data, endPoint);
                    break;
                }
                Header header = (Header)data[0];

                bool shouldSendController = LevelManager.LevelManagerMode != LevelManagerMode.Race || Settings.CheatMode;

                // Send the appropriate response if it was an API call, or break if it was a python_finished message
                byte[] sendData = null;
                switch (header)
                {
                    case Header.error:
                        string errorName = Enum.IsDefined(typeof(Error), (int)data[1]) ? ((Error)data[1]).ToString() : data[1].ToString();
                        this.DropProgram(i, $"Error code [{errorName}] sent from the Python script controlling car {i}.", null);
                        pythonFinished = true;
                        break;

                    case Header.python_finished:
                        pythonFinished = true;
                        break;

                    case Header.python_exit:
                        this.RemoveSyncClientAt(i);
                        pythonFinished = true;
                        break;

                    case Header.racecar_get_delta_time:
                        sendData = BitConverter.GetBytes(Time.deltaTime);
                        break;

                    case Header.camera_get_color_image:
                        pythonFinished = !this.SendFragmented(racecar.Camera.ColorImageRaw, PythonInterface.colorImagePackets, i);
                        break;

                    case Header.camera_get_depth_image:
                        sendData = racecar.Camera.DepthImageRaw;
                        break;

                    case Header.camera_get_width:
                        sendData = BitConverter.GetBytes(CameraModule.ColorWidth);
                        break;

                    case Header.camera_get_height:
                        sendData = BitConverter.GetBytes(CameraModule.ColorHeight);
                        break;

                    // Always return null controller data when in race mode (except in cheat mode)
                    case Header.controller_is_down:
                    case Header.controller_was_pressed:
                    case Header.controller_was_released:
                        if (!Enum.IsDefined(typeof(Controller.Button), (int)data[1]))
                        {
                            this.RejectRequest(data, endPoint);
                            pythonFinished = true;
                            break;
                        }
                        Controller.Button button = (Controller.Button)data[1];
                        bool buttonState =
                            header == Header.controller_is_down ? Controller.IsDown(button) :
                            header == Header.controller_was_pressed ? Controller.WasPressed(button) :
                            Controller.WasReleased(button);
                        sendData = BitConverter.GetBytes(buttonState && shouldSendController);
                        break;

                    case Header.controller_get_trigger:
                        if (!Enum.IsDefined(typeof(Controller.Trigger), (int)data[1]))
                        {
                            this.RejectRequest(data, endPoint);
                            pythonFinished = true;
                            break;
                        }
                        float triggerValue = shouldSendController ? Controller.GetTrigger((Controller.Trigger)data[1]) : 0;
                        sendData = BitConverter.GetBytes(triggerValue);
                        break;

                    case Header.controller_get_joystick:
                        if (!Enum.IsDefined(typeof(Controller.Joystick), (int)data[1]))
                        {
                            this.RejectRequest(data, endPoint);
                            pythonFinished = true;
                            break;
                        }
                        Vector2 joystickValues = shouldSendController ? Controller.GetJoystick((Controller.Joystick)data[1]) : Vector2.zero;
                        sendData = new byte[sizeof(float) * 2];
                        Buffer.BlockCopy(new float[] { joystickValues.x, joystickValues.y }, 0, sendData, 0, sendData.Length);
                        break;

                    case Header.drive_set_speed_angle:
                        racecar.Drive.Speed = BitConverter.ToSingle(data, 4);
                        racecar.Drive.Angle = BitConverter.ToSingle(data, 8);
                        break;

                    case Header.drive_stop:
                        racecar.Drive.Stop();
                        break;

                    case Header.drive_set_max_speed:
                        racecar.Drive.MaxSpeed = BitConverter.ToSingle(data, 4);
                        break;

                    case Header.lidar_get_num_samples:
                        sendData = BitConverter.GetBytes(Lidar.NumSamples);
                        break;

                    case Header.lidar_get_samples:
                        sendData = new byte[sizeof(float) * Lidar.NumSamples];
                        Buffer.BlockCopy(racecar.Lidar.Samples, 0, sendData, 0, sendData.Length);
                        break;

                    case Header.physics_get_linear_acceleration:
                        Vector3 linearAcceleration = racecar.Physics.LinearAcceleration;
                        sendData = new byte[sizeof(float) * 3];
                        Buffer.BlockCopy(new float[] { linearAcceleration.x, linearAcceleration.y, linearAcceleration.z }, 0, sendData, 0, sendData.Length);
                        break;

                    case Header.physics_get_angular_velocity:
                        Vector3 angularVelocity = racecar.Physics.AngularVelocity;
                        sendData = new byte[sizeof(float) * 3];
                        Buffer.BlockCopy(new float[] { angularVelocity.x, angularVelocity.y, angularVelocity.z }, 0, sendData, 0, sendData.Length);
                        break;

                    default:
                        this.RejectRequest(data, endPoint);
                        pythonFinished = true;
                        break;
                }

                if (sendData != null)
                {
                    this.TrySend(this.udpClient, sendData, endPoint);
                }
            }
        }
    }

    /// <summary>
    /// Replies with a generic error to a malformed or unsupported request and logs it.
    /// </summary>
    private void RejectRequest(byte[] data, IPEndPoint endPoint)
    {
        bool known = data != null && data.Length > 0 && Enum.IsDefined(typeof(Header), (int)data[0]);
        if (known && PythonInterface.RequiredLength((Header)data[0]) <= data.Length)
        {
            this.ReportUnsupportedOnce((Header)data[0], "is not supported by RacecarSim, or was sent with invalid arguments");
        }
        else
        {
            string description = data == null || data.Length == 0 ? "an empty packet" : $"a {data.Length}-byte packet with header {data[0]}";
            Debug.LogError($">> Error: Received {description} from Python at [{endPoint}], which is not a valid request.");
        }
        this.TrySend(this.udpClient, PythonInterface.ErrorPacket(Error.generic), endPoint);
    }

    /// <summary>
    /// Sends a large amount of data split across several packets.
    /// </summary>
    /// <param name="bytes">The bytes to send (must be divisible by numPackets).</param>
    /// <param name="numPackets">The number of packets to split the data across.</param>
    /// <param name="index">The index of the car whose Python script receives the data.</param>
    /// <returns>True if the entire message was sent successfully.</returns>
    private bool SendFragmented(byte[] bytes, int numPackets, int index)
    {
        IPEndPoint endPoint = this.pythonEndPoints[index];
        int blockSize = bytes.Length / numPackets;
        byte[] sendData = new byte[blockSize];
        for (int i = 0; i < numPackets; i++)
        {
            Buffer.BlockCopy(bytes, i * blockSize, sendData, 0, blockSize);
            this.TrySend(this.udpClient, sendData, endPoint);

            byte[] response = this.SafeReceive(index);
            if (response == null)
            {
                return false;
            }

            Header responseHeader = response.Length > 0 ? (Header)response[0] : Header.error;
            switch (responseHeader)
            {
                case Header.python_send_next:
                    continue;

                case Header.python_exit:
                    this.RemoveSyncClientAt(index);
                    return false;

                default:
                    this.DropProgram(index, "Unity and Python became out of sync while sending a block message.", Error.fragment_mismatch);
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Receives a packet from the Python script paired with a car, handling UDP exceptions (broken
    /// socket, timeout, etc.) by disconnecting that script.
    /// </summary>
    /// <param name="index">The index of the car whose Python script should respond.</param>
    /// <returns>The data in the packet, or null if an error occurred.</returns>
    private byte[] SafeReceive(int index)
    {
        IPEndPoint expected = this.pythonEndPoints[index];
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            // Discard packets from any other sender until the expected script replies or time runs out
            while (true)
            {
                int remaining = PythonInterface.timeoutTime - (int)timer.ElapsedMilliseconds;
                if (remaining <= 0)
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }
                this.udpClient.Client.ReceiveTimeout = remaining;

                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = this.udpClient.Receive(ref sender);
                if (expected.Equals(sender))
                {
                    return data;
                }
                Debug.LogWarning($"Ignored a packet from [{sender}] while waiting for the Python script controlling car {index}.");
            }
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.TimedOut)
            {
                this.DropProgram(index, "No message received from Python within the alloted time.", Error.timeout);
                Debug.LogError(">> Troubleshooting:" +
                    "\n1. Make sure that your Python program does not block or wait. For example, your program should never call time.sleep()." +
                    "\n2. Make sure that your program is not too computationally intensive. Your start and update functions should be able to run in under 10 milliseconds." +
                    "\n3. Make sure that your Python program did not crash or close unexpectedly." +
                    "\n4. Unless you experience an error, do not force-quit your Python application (ctrl+c or ctrl+d).  Instead, end the simulation by pressing the start and back button simultaneously on your Xbox controller (escape and enter on keyboard).");
            }
            else
            {
                this.DropProgram(index, "An error occurred when attempting to receive data from Python.", Error.generic);
                Debug.LogError($"SocketException: [{e}]");
            }
        }
        return null;
    }

    /// <summary>
    /// Disconnects one Python script after an error, telling it why, and reports the error to the LevelManager.
    /// Other connected scripts stay connected.
    /// </summary>
    /// <param name="index">The index of the car whose script failed.</param>
    /// <param name="errorText">The error text to show.</param>
    /// <param name="errorCode">The error code to send to the script, or null if the script reported the error itself.</param>
    private void DropProgram(int index, string errorText, Error? errorCode)
    {
        if (errorCode.HasValue)
        {
            this.TrySend(this.udpClient, PythonInterface.ErrorPacket(errorCode.Value), this.pythonEndPoints[index]);
        }
        this.RemoveSyncClientAt(index);
        LevelManager.HandleError(errorText);
    }
    #endregion

    #region Async
    /// <summary>
    /// The UDP client used to handle async API calls from Python.
    /// </summary>
    private readonly UdpClient udpClientAsync;

    /// <summary>
    /// A thread containing a UDP client to process asynchronous API calls from Python.
    /// </summary>
    private readonly Thread asyncClientThread;

    /// <summary>
    /// Receives async API calls from Python (for use by Jupyter) until the interface closes.
    /// </summary>
    private void ProcessAsyncCalls()
    {
        while (!this.isClosing)
        {
            IPEndPoint receiveEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try
            {
                data = this.udpClientAsync.Receive(ref receiveEndPoint);
                if (!this.sourceFilter.IsAllowed(receiveEndPoint.Address))
                {
                    this.ReportRejected(receiveEndPoint.Address);
                    continue;
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut && !this.isClosing)
            {
                continue;
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                if (!this.isClosing)
                {
                    Debug.LogError($"Async Python connection closed unexpectedly. Error: {e.Message}");
                }
                return;
            }

            try
            {
                this.HandleAsyncRequest(data, receiveEndPoint);
            }
            catch (Exception e)
            {
                if (!this.isClosing)
                {
                    Debug.LogError($"Unable to handle async request from Python at [{receiveEndPoint}]. Error: {e}");
                    this.TrySend(this.udpClientAsync, PythonInterface.ErrorPacket(Error.generic), receiveEndPoint);
                }
            }
        }
    }

    /// <summary>
    /// Logs and queues an on-screen message the first time a source address is rejected.
    /// </summary>
    private void ReportRejected(IPAddress address)
    {
        if (!this.reportedRejected.Add(address))
        {
            return;
        }
        string message = $"Ignored a Python connection from {address}. RacecarSim accepts programs on this computer and in WSL 2; set {PythonSourceFilter.AllowRemoteVariable}=1 to accept other hosts.";
        Debug.LogWarning(message);
        this.pendingMessages.Enqueue(message);
    }

    /// <summary>
    /// Handles one async request. Runs on the async thread.
    /// </summary>
    private void HandleAsyncRequest(byte[] data, IPEndPoint receiveEndPoint)
    {
        if (!PythonInterface.IsWellFormed(data))
        {
            Debug.LogError($">> Error: Received an invalid async request from Python at [{receiveEndPoint}].");
            this.TrySend(this.udpClientAsync, PythonInterface.ErrorPacket(Error.generic), receiveEndPoint);
            return;
        }

        Header header = (Header)data[0];
        switch (header)
        {
            case Header.connect:
                this.pendingRequests.Enqueue(new PendingRequest()
                {
                    Header = Header.connect,
                    EndPoint = receiveEndPoint,
                    PythonVersion = data.Length > 1 ? data[1] : 0
                });
                return;

            case Header.python_exit:
                this.pendingRequests.Enqueue(new PendingRequest() { Header = Header.python_exit, EndPoint = receiveEndPoint });
                return;
        }

        Racecar racecar = LevelManager.GetCar();
        if (racecar == null)
        {
            this.TrySend(this.udpClientAsync, PythonInterface.ErrorPacket(Error.generic), receiveEndPoint);
            return;
        }

        switch (header)
        {
            case Header.camera_get_color_image:
                this.SendFragmentedAsync(racecar.Camera.GetColorImageRawAsync(), PythonInterface.colorImagePackets, receiveEndPoint);
                break;

            case Header.camera_get_depth_image:
                this.TrySend(this.udpClientAsync, racecar.Camera.GetDepthImageRawAsync(), receiveEndPoint);
                break;

            case Header.lidar_get_samples:
                byte[] sendData = new byte[sizeof(float) * Lidar.NumSamples];
                Buffer.BlockCopy(racecar.Lidar.Samples, 0, sendData, 0, sendData.Length);
                this.TrySend(this.udpClientAsync, sendData, receiveEndPoint);
                break;

            default:
                this.ReportUnsupportedOnce(header, "is not supported by RacecarSim for async calls");
                this.TrySend(this.udpClientAsync, PythonInterface.ErrorPacket(Error.generic), receiveEndPoint);
                break;
        }
    }

    /// <summary>
    /// Sends a large amount of data split across several packets via the async client.
    /// </summary>
    /// <param name="bytes">The bytes to send (must be divisible by numPackets).</param>
    /// <param name="numPackets">The number of packets to split the data across.</param>
    /// <param name="destination">The endpoint of the Python script to which to send data.</param>
    private void SendFragmentedAsync(byte[] bytes, int numPackets, IPEndPoint destination)
    {
        int blockSize = bytes.Length / numPackets;
        byte[] sendData = new byte[blockSize];
        this.udpClientAsync.Client.ReceiveTimeout = PythonInterface.timeoutTime;
        try
        {
            this.SendFragmentsAsync(bytes, numPackets, blockSize, sendData, destination);
        }
        finally
        {
            this.udpClientAsync.Client.ReceiveTimeout = PythonInterface.asyncPollTime;
        }
    }

    /// <summary>
    /// Sends each fragment and waits for Python to request the next.
    /// </summary>
    private void SendFragmentsAsync(byte[] bytes, int numPackets, int blockSize, byte[] sendData, IPEndPoint destination)
    {
        for (int i = 0; i < numPackets; i++)
        {
            Buffer.BlockCopy(bytes, i * blockSize, sendData, 0, blockSize);
            this.TrySend(this.udpClientAsync, sendData, destination);

            byte[] response;
            try
            {
                response = this.udpClientAsync.Receive(ref destination);
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
            {
                response = null;
            }

            if (response == null || response.Length == 0 || (Header)response[0] != Header.python_send_next)
            {
                this.TrySend(this.udpClientAsync, PythonInterface.ErrorPacket(Error.fragment_mismatch), destination);
                break;
            }
        }
    }
    #endregion
}
