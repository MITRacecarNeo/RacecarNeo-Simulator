using System;
using System.Globalization;

/// <summary>
/// Where a physical car's link listens: its address, bridge port, and API port.
/// </summary>
[Serializable]
public class CarLinkTarget
{
    #region Constants
    /// <summary>
    /// The car's address on its own access point.
    /// </summary>
    public const string DefaultHost = "10.42.0.1";

    public const int DefaultBridgePort = 9090;

    public const int DefaultApiPort = 8084;
    #endregion

    #region Public Interface
    public string Host = CarLinkTarget.DefaultHost;
    public int BridgePort = CarLinkTarget.DefaultBridgePort;
    public int ApiPort = CarLinkTarget.DefaultApiPort;

    /// <summary>
    /// The car's hostname from its last successful connection, for the recent list; may be empty.
    /// </summary>
    public string Name = string.Empty;

    /// <summary>
    /// The bridge's WebSocket address.
    /// </summary>
    public Uri BridgeUri
    {
        get { return new UriBuilder("ws", this.Host, this.BridgePort).Uri; }
    }

    /// <summary>
    /// The address with ports that differ from the defaults, for display.
    /// </summary>
    public string Label
    {
        get
        {
            string label = this.Host;
            if (this.BridgePort != CarLinkTarget.DefaultBridgePort || this.ApiPort != CarLinkTarget.DefaultApiPort)
            {
                label += $":{this.BridgePort}:{this.ApiPort}";
            }
            return label;
        }
    }

    /// <summary>
    /// Parses "host", "host:bridgePort", or "host:bridgePort:apiPort".
    /// </summary>
    /// <returns>False when the host is empty or holds spaces, or a port is not 1 to 65535.</returns>
    public static bool TryParse(string text, out CarLinkTarget target)
    {
        target = null;
        string[] parts = (text ?? string.Empty).Trim().Split(':');
        if (parts.Length > 3)
        {
            return false;
        }
        CarLinkTarget parsed = new CarLinkTarget { Host = parts[0] };
        if (!CarLinkTarget.IsValidHost(parsed.Host))
        {
            return false;
        }
        if (parts.Length > 1 && !CarLinkTarget.TryParsePort(parts[1], out parsed.BridgePort))
        {
            return false;
        }
        if (parts.Length > 2 && !CarLinkTarget.TryParsePort(parts[2], out parsed.ApiPort))
        {
            return false;
        }
        target = parsed;
        return true;
    }

    /// <summary>
    /// True for an address or name without spaces or a URL scheme.
    /// </summary>
    public static bool IsValidHost(string host)
    {
        if (string.IsNullOrEmpty(host) || host.Contains("/"))
        {
            return false;
        }
        foreach (char c in host)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }
        return Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }

    /// <summary>
    /// Parses a TCP port from 1 to 65535.
    /// </summary>
    public static bool TryParsePort(string text, out int port)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port >= 1 && port <= 65535;
    }

    /// <summary>
    /// True when both point at the same car and ports.
    /// </summary>
    public bool SameAddress(CarLinkTarget other)
    {
        return other != null && string.Equals(this.Host, other.Host, StringComparison.OrdinalIgnoreCase)
            && this.BridgePort == other.BridgePort && this.ApiPort == other.ApiPort;
    }
    #endregion
}
