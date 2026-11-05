using MauiNetwork = Microsoft.Maui.Networking;

namespace Ohmline.Mobile.Platform;

/// <summary>
/// The app's <see cref="Core.Platform.IConnectivity"/>, adapted from MAUI's: online when the
/// phone reports internet access, captive portals included, because only a send attempt can
/// tell for sure. Android needs the <c>ACCESS_NETWORK_STATE</c> permission for it.
/// </summary>
public sealed class MauiConnectivity : Core.Platform.IConnectivity
{
    private readonly MauiNetwork.IConnectivity _connectivity = MauiNetwork.Connectivity.Current;

    public MauiConnectivity() =>
        _connectivity.ConnectivityChanged +=
            (_, _) => ConnectivityChanged?.Invoke(this, EventArgs.Empty);

    public bool IsOnline => _connectivity.NetworkAccess
        is MauiNetwork.NetworkAccess.Internet or MauiNetwork.NetworkAccess.ConstrainedInternet;

    public event EventHandler? ConnectivityChanged;
}
