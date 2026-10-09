using UnityEngine;

/// <summary>
/// The latest value of each car sensor that Python can read. Sensor modules write on the main
/// thread; PythonInterface reads on the main thread (sync calls) and on the async thread (Jupyter
/// calls), so every access takes the lock.
/// </summary>
public class SensorReadings
{
    private readonly object sync = new object();
    private Vector3 linearAcceleration;
    private Vector3 angularVelocity;
    private Vector3 magneticField;
    private Vector3 legacyLinearAcceleration;
    private Vector3 legacyAngularVelocity;
    private float encoderSpeed;
    private float batteryVoltage;
    private float batteryCurrent;

    /// <summary>
    /// Linear acceleration (m/s^2), in the frame of PhysicsModule.LinearAcceleration.
    /// </summary>
    public Vector3 LinearAcceleration
    {
        get { lock (this.sync) { return this.linearAcceleration; } }
        set { lock (this.sync) { this.linearAcceleration = value; } }
    }

    /// <summary>
    /// Angular velocity (rad/s), in the frame of PhysicsModule.AngularVelocity.
    /// </summary>
    public Vector3 AngularVelocity
    {
        get { lock (this.sync) { return this.angularVelocity; } }
        set { lock (this.sync) { this.angularVelocity = value; } }
    }

    /// <summary>
    /// Linear acceleration in the frame of PhysicsModule.LegacyLinearAcceleration, for protocol
    /// version 1 clients.
    /// </summary>
    public Vector3 LegacyLinearAcceleration
    {
        get { lock (this.sync) { return this.legacyLinearAcceleration; } }
        set { lock (this.sync) { this.legacyLinearAcceleration = value; } }
    }

    /// <summary>
    /// Angular velocity in the frame of PhysicsModule.LegacyAngularVelocity, for protocol version 1
    /// clients.
    /// </summary>
    public Vector3 LegacyAngularVelocity
    {
        get { lock (this.sync) { return this.legacyAngularVelocity; } }
        set { lock (this.sync) { this.legacyAngularVelocity = value; } }
    }

    /// <summary>
    /// Magnetic field (Tesla), REP-103 body axes.
    /// </summary>
    public Vector3 MagneticField
    {
        get { lock (this.sync) { return this.magneticField; } }
        set { lock (this.sync) { this.magneticField = value; } }
    }

    /// <summary>
    /// Drive encoder speed (m/s), positive forward.
    /// </summary>
    public float EncoderSpeed
    {
        get { lock (this.sync) { return this.encoderSpeed; } }
        set { lock (this.sync) { this.encoderSpeed = value; } }
    }

    /// <summary>
    /// Battery bus voltage (V).
    /// </summary>
    public float BatteryVoltage
    {
        get { lock (this.sync) { return this.batteryVoltage; } }
        set { lock (this.sync) { this.batteryVoltage = value; } }
    }

    /// <summary>
    /// Battery current draw (A), never negative.
    /// </summary>
    public float BatteryCurrent
    {
        get { lock (this.sync) { return this.batteryCurrent; } }
        set { lock (this.sync) { this.batteryCurrent = value; } }
    }
}
