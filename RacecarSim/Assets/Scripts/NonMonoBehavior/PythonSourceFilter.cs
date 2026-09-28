using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// Decides which hosts may talk to the simulator. The UDP sockets bind to every interface so that
/// Python in WSL 2 (NAT mode) can connect, but only packets from this computer and from WSL 2
/// virtual adapters are accepted. Setting the environment variable RACECARSIM_ALLOW_REMOTE=1
/// accepts every host.
/// </summary>
public class PythonSourceFilter
{
    /// <summary>
    /// Environment variable that, when set to 1, accepts packets from any host.
    /// </summary>
    public const string AllowRemoteVariable = "RACECARSIM_ALLOW_REMOTE";

    /// <summary>
    /// Subnet mask assumed for a WSL adapter when the platform does not report one.
    /// </summary>
    private static readonly IPAddress defaultWslMask = IPAddress.Parse("255.255.240.0");

    /// <summary>
    /// Minimum time between rescans of the network adapters, in milliseconds.
    /// </summary>
    private const long rescanInterval = 5000;

    /// <summary>
    /// True when every host is accepted.
    /// </summary>
    public bool AllowRemote { get; }

    /// <summary>
    /// The (network address, mask) pairs of WSL virtual adapters found in the last scan.
    /// </summary>
    private List<(IPAddress address, IPAddress mask)> wslSubnets = new List<(IPAddress, IPAddress)>();

    /// <summary>
    /// Time since the last adapter scan; null before the first scan.
    /// </summary>
    private Stopwatch sinceScan;

    /// <summary>
    /// Guards wslSubnets and sinceScan; IsAllowed is called from the async thread and the main thread.
    /// </summary>
    private readonly object scanLock = new object();

    public PythonSourceFilter()
        : this(Environment.GetEnvironmentVariable(PythonSourceFilter.AllowRemoteVariable) == "1")
    {
    }

    /// <summary>
    /// Creates a filter with an explicit remote policy.
    /// </summary>
    /// <param name="allowRemote">True to accept packets from any host.</param>
    public PythonSourceFilter(bool allowRemote)
    {
        this.AllowRemote = allowRemote;
    }

    /// <summary>
    /// Returns whether a packet from the given address should be handled. Rescans the network
    /// adapters (at most every 5 s) before rejecting, since WSL may start after the simulator.
    /// </summary>
    /// <param name="address">The source address of the packet.</param>
    /// <returns>True for loopback, WSL 2 adapters, or any host when AllowRemote is set.</returns>
    public bool IsAllowed(IPAddress address)
    {
        if (this.AllowRemote || IPAddress.IsLoopback(address))
        {
            return true;
        }

        lock (this.scanLock)
        {
            if (this.IsInWslSubnet(address))
            {
                return true;
            }

            if (this.sinceScan == null || this.sinceScan.ElapsedMilliseconds > PythonSourceFilter.rescanInterval)
            {
                this.wslSubnets = PythonSourceFilter.FindWslSubnets();
                this.sinceScan = Stopwatch.StartNew();
                return this.IsInWslSubnet(address);
            }
        }
        return false;
    }

    /// <summary>
    /// Returns whether an IPv4 address lies in the subnet given by a network address and mask.
    /// </summary>
    /// <param name="address">The address to test.</param>
    /// <param name="network">Any address in the subnet.</param>
    /// <param name="mask">The subnet mask.</param>
    /// <returns>True if both addresses are IPv4 and share the masked prefix.</returns>
    public static bool IsInSubnet(IPAddress address, IPAddress network, IPAddress mask)
    {
        byte[] a = address.GetAddressBytes();
        byte[] n = network.GetAddressBytes();
        byte[] m = mask.GetAddressBytes();
        if (a.Length != 4 || n.Length != 4 || m.Length != 4)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            if ((a[i] & m[i]) != (n[i] & m[i]))
            {
                return false;
            }
        }
        return true;
    }

    private bool IsInWslSubnet(IPAddress address)
    {
        return this.wslSubnets.Any(subnet => PythonSourceFilter.IsInSubnet(address, subnet.address, subnet.mask));
    }

    /// <summary>
    /// Finds the IPv4 subnets of network adapters whose name contains "WSL" (for example
    /// "vEthernet (WSL)"). Returns an empty list on platforms without WSL.
    /// </summary>
    private static List<(IPAddress, IPAddress)> FindWslSubnets()
    {
        List<(IPAddress, IPAddress)> subnets = new List<(IPAddress, IPAddress)>();
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.Name.IndexOf("WSL", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation info in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    IPAddress mask;
                    try
                    {
                        mask = info.IPv4Mask ?? PythonSourceFilter.defaultWslMask;
                    }
                    catch (NotImplementedException)
                    {
                        mask = PythonSourceFilter.defaultWslMask;
                    }
                    subnets.Add((info.Address, mask));
                }
            }
        }
        catch (NetworkInformationException e)
        {
            UnityEngine.Debug.LogWarning($"Unable to list network adapters for WSL detection: {e.Message}");
        }
        return subnets;
    }
}
