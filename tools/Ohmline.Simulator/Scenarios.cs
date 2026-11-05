using System.Globalization;
using Ohmline.DeviceGateway.Ocpp.Messages;

namespace Ohmline.Simulator;

/// <summary>What became of one simulated charger: its charge point, session and end.</summary>
public sealed record ScenarioResult(string ChargePointId, Guid? SessionId, string Outcome);

/// <summary>
/// What a simulated charger and its driver do. Each scenario sets up a charge point of its own
/// (<c>OHM-SIM-nnnn</c>), so several run side by side and runs never collide.
/// </summary>
public static class Scenarios
{
    private const long MeterStart = 1_200_000;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    public static Task<ScenarioResult> RunAsync(
        SimulatorOptions options, int number, CancellationToken cancellationToken) =>
        options.Scenario switch
        {
            SimulatorOptions.PlugInAndCharge => PlugInAndChargeAsync(
                options, number, cancellationToken),
            SimulatorOptions.Fault => FaultAsync(options, number, cancellationToken),
            SimulatorOptions.Disconnect => DisconnectAsync(options, number, cancellationToken),
            var other => throw new ArgumentException($"There is no scenario '{other}'."),
        };

    /// <summary>
    /// The whole charge: the driver starts a journey, the charger is told to start, the car
    /// draws 12.5 kWh in three readings and is unplugged, and the journey is settled.
    /// </summary>
    public static async Task<ScenarioResult> PlugInAndChargeAsync(
        SimulatorOptions options, int number, CancellationToken cancellationToken)
    {
        var (charger, driver, token) = await ArriveAsync(options, number, cancellationToken);
        await using (charger)
        {
            var (session, transaction) = await ChargeAsync(
                charger, driver, token, cancellationToken);
            var register = await MeterAsync(charger, transaction, cancellationToken);
            await charger.CallAsync(
                OcppActions.StopTransaction,
                new StopTransactionRequest(
                    transaction, register, DateTimeOffset.UtcNow, Reason: "EVDisconnected"),
                cancellationToken);
            await charger.StatusAsync(1, "Finishing", cancellationToken);
            await charger.StatusAsync(1, "Available", cancellationToken);
            var outcome = await driver.WaitForStepAsync(
                session, "completed", Patience, cancellationToken);
            return new ScenarioResult(charger.ChargePointId, session, outcome);
        }
    }

    /// <summary>A connector fails and is repaired, with no car on it.</summary>
    public static async Task<ScenarioResult> FaultAsync(
        SimulatorOptions options, int number, CancellationToken cancellationToken)
    {
        var (charger, _, _) = await ArriveAsync(options, number, cancellationToken);
        await using (charger)
        {
            await charger.StatusAsync(1, "Faulted", cancellationToken, error: "GroundFailure");
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            await charger.StatusAsync(1, "Available", cancellationToken);
            return new ScenarioResult(charger.ChargePointId, null, "fault-cleared");
        }
    }

    /// <summary>The charger loses its network in the middle of a session.</summary>
    public static async Task<ScenarioResult> DisconnectAsync(
        SimulatorOptions options, int number, CancellationToken cancellationToken)
    {
        var (charger, driver, token) = await ArriveAsync(options, number, cancellationToken);
        await using (charger)
        {
            var (session, _) = await ChargeAsync(charger, driver, token, cancellationToken);
            charger.Drop();
            return new ScenarioResult(charger.ChargePointId, session, "disconnected");
        }
    }

    private static async Task<(SimulatedCharger, Driver, string)> ArriveAsync(
        SimulatorOptions options, int number, CancellationToken cancellationToken)
    {
        var suffix = (Random.Shared.Next(0, 9_000) + number) % 10_000;
        var chargePointId = $"OHM-SIM-{suffix.ToString("0000", CultureInfo.InvariantCulture)}";
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..7]);
        var driver = new Driver(new HttpClient { BaseAddress = options.Node });
        await driver.SetUpAsync(chargePointId, $"sim-{suffix}", token, cancellationToken);

        var charger = new SimulatedCharger(chargePointId);
        await charger.ConnectAsync(options.Gateway, cancellationToken);
        await charger.CallAsync(
            OcppActions.BootNotification,
            new BootNotificationRequest("Ohmline", "Simulator", "16.7"),
            cancellationToken);
        await charger.StatusAsync(1, "Available", cancellationToken);
        return (charger, driver, token);
    }

    /// <summary>
    /// The driver starts a journey; the gateway tells the charger to start; the car draws.
    /// Returns the session, which the journey named, and its OCPP transaction.
    /// </summary>
    private static async Task<(Guid Session, int Transaction)> ChargeAsync(
        SimulatedCharger charger, Driver driver, string token, CancellationToken cancellationToken)
    {
        var journey = await driver.StartJourneyAsync(
            token, $"{charger.ChargePointId}/1", cancellationToken);
        await charger.RemoteStart.Task.WaitAsync(Patience, cancellationToken);
        await charger.StatusAsync(1, "Preparing", cancellationToken);
        var started = await charger.CallAsync(
            OcppActions.StartTransaction,
            new StartTransactionRequest(1, token, MeterStart, DateTimeOffset.UtcNow),
            cancellationToken);
        await charger.StatusAsync(1, "Charging", cancellationToken);
        await driver.WaitForStepAsync(journey, "charging", Patience, cancellationToken);
        return (journey, started.GetProperty("transactionId").GetInt32());
    }

    /// <summary>Three readings, 12.5 kWh in all; the last register.</summary>
    private static async Task<long> MeterAsync(
        SimulatedCharger charger, int transaction, CancellationToken cancellationToken)
    {
        var register = MeterStart;
        foreach (var wattHours in new[] { 5_000L, 5_000L, 2_500L })
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            register += wattHours;
            await charger.CallAsync(
                OcppActions.MeterValues,
                new MeterValuesRequest(
                    1,
                    transaction,
                    [new MeterValue(
                        DateTimeOffset.UtcNow,
                        [new SampledValue(
                            register.ToString(CultureInfo.InvariantCulture), Unit: "Wh")])]),
                cancellationToken);
        }

        return register;
    }
}
