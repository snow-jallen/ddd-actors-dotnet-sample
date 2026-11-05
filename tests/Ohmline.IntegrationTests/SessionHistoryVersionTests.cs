using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Ohmline.Charging.Api.Queries;

namespace Ohmline.IntegrationTests;

/// <summary>
/// Both versions of the session history are built by every node (section 20.9), and one
/// setting, <c>Ohmline:ReadModels:SessionHistoryVersion</c>, says which one answers: the
/// switch from the first to the second, and back, is a configuration change, not a migration.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class SessionHistoryVersionTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1, null)]
    [InlineData(2, "slc-depot")]
    public async Task Given_a_version_then_its_table_answers(int version, string? siteId)
    {
        await using var node = new OhmlineNode(postgres).WithWebHostBuilder(builder =>
            builder.UseSetting("Ohmline:ReadModels:SessionHistoryVersion", $"{version}"));
        var http = node.CreateClient();
        var session = Guid.CreateVersion7();
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/sessions", Sessions.Start(session), Ct))
            .EnsureSuccessStatusCode();

        var answer = await http.GetAsync($"/sessions/{session}?minVersion=1", Ct);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = (await answer.Content.ReadFromJsonAsync<SessionResponse>(Ct)).ShouldNotBeNull();
        row.SessionId.ShouldBe(session);
        row.SiteId.ShouldBe(siteId);
    }
}
