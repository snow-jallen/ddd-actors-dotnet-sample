using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Mobile.Core.Api;
using Ohmline.Mobile.Core.Platform;

namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// The start-charging screen (section 19.5): the driver scans or types a connector's code and
/// chooses a spend cap. The value objects turn bad input into sentences before any command
/// exists; a valid tap becomes a <see cref="StartChargingRequest"/> with ids made at once, and
/// the server's answer becomes a <see cref="CommandStatus"/> in the driver's words.
/// </summary>
public sealed partial class StartChargingViewModel(
    ChargingApiClient api, DriverToken token, IConnectivity connectivity, TimeProvider clock)
    : ObservableObject
{
    /// <summary>What the driver scanned or typed: <c>OHM-SLC-0042/1</c>.</summary>
    [ObservableProperty]
    public partial string ConnectorText { get; set; } = "";

    [ObservableProperty]
    public partial string? ConnectorError { get; set; }

    [ObservableProperty]
    public partial string SpendCapText { get; set; } = "40";

    [ObservableProperty]
    public partial string? SpendCapError { get; set; }

    [ObservableProperty]
    public partial ScreenState Screen { get; set; } = new Ready();

    /// <summary>The last command the screen gave; none before the first tap.</summary>
    [ObservableProperty]
    public partial CommandStatus? Status { get; set; }

    [RelayCommand]
    private async Task StartChargingAsync(CancellationToken cancellationToken)
    {
        if (Validated() is not { } request)
        {
            return;
        }

        Screen = new Loading();
        Status = new Sending(1);
        try
        {
            Status = await api.StartChargingAsync(request, cancellationToken);
            Screen = new Ready();
        }
        catch (HttpRequestException)
        {
            Status = null;
            Screen = connectivity.IsOnline
                ? new Failed("Ohmline did not answer. Try again in a moment.")
                : new Offline();
        }
    }

    /// <summary>
    /// The request, if the input is valid, with both ids made now, at the tap: a version 7 Guid
    /// sorts by time, in the server's journal and in the phone's logs. Otherwise null, and each
    /// field that is not valid says why.
    /// </summary>
    private StartChargingRequest? Validated()
    {
        var connector = ConnectorCode.TryParse(ConnectorText);
        var spendCap = SpendCap.TryParse(SpendCapText);
        ConnectorError = connector.Value is Invalid badCode ? badCode.Message : null;
        SpendCapError = spendCap.Value is Invalid badCap ? badCap.Message : null;
        if ((connector.Value, spendCap.Value) is not
            (Parsed<ConnectorCode> code, Parsed<SpendCap> cap))
        {
            return null;
        }

        var now = clock.GetUtcNow();
        return new StartChargingRequest(
            CommandId: Guid.CreateVersion7(now),
            JourneyId: Guid.CreateVersion7(now),
            ConnectorId: code.Value.ToString(),
            TokenUid: token.Uid,
            SpendCapAmount: cap.Value.Amount,
            Currency: SpendCap.Currency);
    }
}
