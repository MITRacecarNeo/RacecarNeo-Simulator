using UnityEngine;

/// <summary>
/// The RACECAR Neo V2 battery (2S LiPo, 5000 mAh) as its INA226 power sensor reports it: bus
/// voltage and current draw.
/// </summary>
/// <remarks>
/// Charge is counted in coulombs from the current draw. The open-circuit voltage follows the
/// drive board firmware's charge table (8.4 V full, 7.0 V empty), and the bus voltage sags under
/// load. Current: 2.5 A at idle (2 hours from full), 5 A to 10 A while the drive is commanded,
/// rising with speed (1 hour to 30 minutes from full).
/// </remarks>
public class BatteryModel
{
    #region Constants
    /// <summary>
    /// Pack capacity (in amp-hours).
    /// </summary>
    public const float CapacityAh = 5.0f;

    /// <summary>
    /// Current draw with the drive idle (in amps).
    /// </summary>
    public const float IdleCurrent = 2.5f;

    /// <summary>
    /// Current draw with the drive commanded, at a standstill (in amps).
    /// </summary>
    public const float DriveCurrentMin = 5.0f;

    /// <summary>
    /// Current draw with the drive commanded at FullDriveSpeed or faster (in amps).
    /// </summary>
    public const float DriveCurrentMax = 10.0f;

    /// <summary>
    /// Speed at which the drive draws DriveCurrentMax (in m/s): full command at max speed 1.0.
    /// </summary>
    public const float FullDriveSpeed = 4.0f;

    /// <summary>
    /// Resistance of the pack, wiring, and connectors (in ohms): the bus sags 0.15 V at 10 A
    /// relative to idle.
    /// </summary>
    public const float SagResistance = 0.02f;

    /// <summary>
    /// Bus voltage at full charge and idle current (in volts).
    /// </summary>
    public const float FullVoltage = 8.4f;

    /// <summary>
    /// Open-circuit voltage at empty (in volts).
    /// </summary>
    public const float EmptyVoltage = 7.0f;

    /// <summary>
    /// The firmware's charge table (racecar-pit-firmware battery.cpp): open-circuit millivolts and
    /// percent charge, linear between points.
    /// </summary>
    private static readonly (float Millivolts, float Percent)[] chargeTable =
    {
        (8400, 100), (8200, 88), (8000, 78), (7800, 64), (7700, 55),
        (7600, 40), (7500, 25), (7400, 13), (7200, 4), (7000, 0),
    };

    /// <summary>
    /// INA226 bus voltage resolution as packed by the firmware (in volts).
    /// </summary>
    private const float voltageStep = 0.001f;

    /// <summary>
    /// INA226 current resolution as packed by the firmware (in amps).
    /// </summary>
    private const float currentStep = 0.001f;

    /// <summary>
    /// Standard deviation of the voltage reading with realism on (in volts).
    /// </summary>
    private const float voltageNoise = 0.005f;

    /// <summary>
    /// Standard deviation of the current reading with realism on (in amps).
    /// </summary>
    private const float currentNoise = 0.02f;
    #endregion

    #region Public Interface
    /// <summary>
    /// Remaining charge, from 1 (full) to 0 (empty).
    /// </summary>
    public float Charge
    {
        get { return (float)this.charge; }
    }

    /// <summary>
    /// True once the charge reaches 0.
    /// </summary>
    public bool IsEmpty
    {
        get { return this.Charge <= 0; }
    }

    /// <summary>
    /// Current draw in the last step (in amps).
    /// </summary>
    public float Current { get; private set; } = BatteryModel.IdleCurrent;

    /// <summary>
    /// Bus voltage in the last step (in volts), sagging under load.
    /// </summary>
    public float Voltage
    {
        get { return BatteryModel.OpenCircuitVoltage(this.Charge) - (this.Current - BatteryModel.IdleCurrent) * BatteryModel.SagResistance; }
    }

    /// <summary>
    /// Current draw (in amps) for a drive state.
    /// </summary>
    /// <param name="isDriving">True while the drive is commanded (nonzero speed).</param>
    /// <param name="speed">Wheel speed (in m/s); its magnitude sets the driving current.</param>
    public static float DrawCurrent(bool isDriving, float speed)
    {
        if (!isDriving)
        {
            return BatteryModel.IdleCurrent;
        }
        float fraction = Mathf.Min(Mathf.Abs(speed) / BatteryModel.FullDriveSpeed, 1);
        return BatteryModel.DriveCurrentMin + fraction * (BatteryModel.DriveCurrentMax - BatteryModel.DriveCurrentMin);
    }

    /// <summary>
    /// Open-circuit voltage (in volts) at a charge from 0 to 1, from the firmware's charge table.
    /// </summary>
    public static float OpenCircuitVoltage(float charge)
    {
        float percent = Mathf.Clamp01(charge) * 100;
        for (int i = 1; i < BatteryModel.chargeTable.Length; i++)
        {
            (float highMv, float highPercent) = BatteryModel.chargeTable[i - 1];
            (float lowMv, float lowPercent) = BatteryModel.chargeTable[i];
            if (percent >= lowPercent)
            {
                float t = (percent - lowPercent) / (highPercent - lowPercent);
                return Mathf.Lerp(lowMv, highMv, t) / 1000;
            }
        }
        return BatteryModel.EmptyVoltage;
    }

    /// <summary>
    /// Charge from 0 to 1 that the firmware's table assigns to a voltage, as the drive board does
    /// for its idle battery display.
    /// </summary>
    public static float ChargeFromVoltage(float volts)
    {
        float millivolts = volts * 1000;
        if (millivolts >= BatteryModel.chargeTable[0].Millivolts)
        {
            return 1;
        }
        for (int i = 1; i < BatteryModel.chargeTable.Length; i++)
        {
            (float highMv, float highPercent) = BatteryModel.chargeTable[i - 1];
            (float lowMv, float lowPercent) = BatteryModel.chargeTable[i];
            if (millivolts >= lowMv)
            {
                return Mathf.Lerp(lowPercent, highPercent, (millivolts - lowMv) / (highMv - lowMv)) / 100;
            }
        }
        return 0;
    }

    /// <summary>
    /// Drains the battery for one step.
    /// </summary>
    /// <param name="deltaTime">Step length (in seconds).</param>
    /// <param name="isDriving">True while the drive is commanded (nonzero speed).</param>
    /// <param name="speed">Wheel speed (in m/s).</param>
    public void Step(float deltaTime, bool isDriving, float speed)
    {
        this.Current = BatteryModel.DrawCurrent(isDriving, speed);
        this.charge = System.Math.Max(0, this.charge - (double)this.Current * deltaTime / 3600 / BatteryModel.CapacityAh);
    }

    /// <summary>
    /// The voltage reading the power sensor reports (in volts): 1 mV resolution, plus noise with
    /// realism on.
    /// </summary>
    public float ReadVoltage(bool isRealism)
    {
        float volts = this.Voltage + (isRealism ? NormalDist.Random(0, BatteryModel.voltageNoise) : 0);
        return Mathf.Round(volts / BatteryModel.voltageStep) * BatteryModel.voltageStep;
    }

    /// <summary>
    /// The current reading the power sensor reports (in amps): 1 mA resolution, never negative,
    /// plus noise with realism on.
    /// </summary>
    public float ReadCurrent(bool isRealism)
    {
        float amps = this.Current + (isRealism ? NormalDist.Random(0, BatteryModel.currentNoise) : 0);
        return Mathf.Max(0, Mathf.Round(amps / BatteryModel.currentStep) * BatteryModel.currentStep);
    }
    #endregion

    /// <summary>
    /// Remaining charge from 1 to 0, in double precision: each physics step removes about 3e-6,
    /// which a float near 1 would round.
    /// </summary>
    private double charge = 1;
}
