using System.Text.Json;
using System.Text.Json.Serialization;
using Ohmline.Charging.Client.Contracts.Notifications;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Charging.Client.Contracts.Requests;

namespace Ohmline.Charging.Client.Contracts;

/// <summary>
/// The JSON of every client DTO, written at compile time by the System.Text.Json source
/// generator: no reflection at run time, so it survives trimming and NativeAOT on a phone. The
/// server's <c>/app/v1</c> endpoints use it too, so both ends read and write the same shapes, in
/// the web defaults (camelCase names) that ASP.NET Core uses.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StartChargingRequest))]
[JsonSerializable(typeof(StopChargingRequest))]
[JsonSerializable(typeof(ReserveConnectorRequest))]
[JsonSerializable(typeof(CommandAcceptedDto))]
[JsonSerializable(typeof(ProblemDto))]
[JsonSerializable(typeof(DriverSessionCard))]
[JsonSerializable(typeof(DriverSessionCards))]
[JsonSerializable(typeof(DriverFeedEntry))]
[JsonSerializable(typeof(DriverFeedPage))]
[JsonSerializable(typeof(ChargePointListingDto))]
[JsonSerializable(typeof(SessionSummaryChanged))]
[JsonSerializable(typeof(DriverCardChanged))]
public sealed partial class ChargingClientJsonContext : JsonSerializerContext;
