namespace Ohmline.Mobile.Core.Platform;

/// <summary>
/// Whether the phone seems to be online, and when that changes: the app's own small port,
/// which the MAUI head adapts from <c>Microsoft.Maui.Networking.Connectivity</c>, so this
/// library needs no MAUI reference and a test can play the network.
/// </summary>
/// <remarks>
/// A hint, never proof: a phone on a captive Wi-Fi or behind a router with no uplink says it is
/// online. Only a send attempt shows whether the server can be reached.
/// </remarks>
public interface IConnectivity
{
    bool IsOnline { get; }

    event EventHandler? ConnectivityChanged;
}
