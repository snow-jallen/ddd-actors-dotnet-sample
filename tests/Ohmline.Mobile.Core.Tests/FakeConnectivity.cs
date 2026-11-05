using Ohmline.Mobile.Core.Platform;

namespace Ohmline.Mobile.Core.Tests;

/// <summary>The phone's network as a test sets it; changing it raises the event.</summary>
internal sealed class FakeConnectivity(bool online = true) : IConnectivity
{
    public bool IsOnline
    {
        get => online;
        set
        {
            online = value;
            ConnectivityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ConnectivityChanged;
}
