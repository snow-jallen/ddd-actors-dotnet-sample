using Ohmline.Simulator;

// dotnet run --project tools/Ohmline.Simulator -- --chargers 5 --scenario plug-in-and-charge
var options = SimulatorOptions.Parse(args, Environment.GetEnvironmentVariables());
using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, cancel) =>
{
    cancel.Cancel = true;
    stopping.Cancel();
};

Console.WriteLine(
    $"{options.Chargers} charger(s), scenario {options.Scenario}, gateway {options.Gateway}, " +
    $"node {options.Node}");
var results = await Task.WhenAll(Enumerable.Range(1, options.Chargers)
    .Select(number => Scenarios.RunAsync(options, number, stopping.Token)));
foreach (var result in results)
{
    Console.WriteLine($"{result.ChargePointId}: session {result.SessionId}, {result.Outcome}");
}

return results.All(result => result.Outcome is not ("abandoned" or "unknown")) ? 0 : 1;
