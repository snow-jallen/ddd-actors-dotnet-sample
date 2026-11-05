using System.Text.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Notifications;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Testing;

namespace Ohmline.ContractTests;

/// <summary>
/// The driver app's published language (section 19.2): every client DTO as golden JSON, written
/// and read with the source-generated <see cref="ChargingClientJsonContext"/> that both the
/// server and the app use. Apps stay in drivers' pockets for months, so a shape that changes
/// here breaks phones that cannot be updated: add fields, never rename or remove one.
/// </summary>
public sealed class ClientContractsTests
{
    public static TheoryData<string> Dtos => new(ClientSamples.Dtos.Keys);

    [Fact]
    public void Every_client_DTO_has_a_sample()
    {
        var types = typeof(ChargingClientJsonContext).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && type != typeof(ChargingClientJsonContext));

        types.ShouldBe(
            ClientSamples.Dtos.Values.Select(sample => sample.GetType()), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void Every_client_DTO_is_in_the_generated_JSON_context(string name)
    {
        var type = ClientSamples.Dtos[name].GetType();

        ChargingClientJsonContext.Default.GetTypeInfo(type).ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void A_client_DTO_is_written_as_its_golden_file_says(string name)
    {
        var sample = ClientSamples.Dtos[name];

        Golden.Verify(
            $"client/{name}",
            JsonSerializer.Serialize(sample, sample.GetType(), ChargingClientJsonContext.Default));
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void The_golden_file_reads_back_and_writes_the_same(string name)
    {
        var type = ClientSamples.Dtos[name].GetType();
        var golden = Golden.Read($"client/{name}");

        var read = JsonSerializer.Deserialize(golden, type, ChargingClientJsonContext.Default);

        Golden.Verify(
            $"client/{name}",
            JsonSerializer.Serialize(read, type, ChargingClientJsonContext.Default));
    }

    [Fact]
    public void An_app_reads_an_answer_with_a_field_it_does_not_know_yet()
    {
        const string fromANewerServer = """
            {"commandId":"019a4f6e-1c40-7c11-9a2b-3c4d00000c01","version":1,"acceptedAt":"now"}
            """;

        JsonSerializer.Deserialize(
                fromANewerServer, ChargingClientJsonContext.Default.CommandAcceptedDto)
            .ShouldBe(new CommandAcceptedDto(ClientSamples.Command, 1));
    }

    [Fact]
    public void An_app_reads_a_push_from_a_server_that_sends_no_trace_parent()
    {
        var push = JsonSerializer.Serialize(
            new { position = 2, card = ClientSamples.Card },
            JsonSerializerOptions.Web);

        JsonSerializer.Deserialize(push, ChargingClientJsonContext.Default.DriverCardChanged)
            .ShouldBe(new DriverCardChanged(2, ClientSamples.Card));
    }

    [Fact]
    public void An_app_reads_a_problem_without_a_code()
    {
        const string fromAProxy = """{"title":"Bad Gateway","status":502}""";

        JsonSerializer.Deserialize(fromAProxy, ChargingClientJsonContext.Default.ProblemDto)
            .ShouldBe(new ProblemDto(null, "Bad Gateway", 502, null, null));
    }
}
