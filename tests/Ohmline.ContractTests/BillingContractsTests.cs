using System.Text.Json;
using Ohmline.Billing.Contracts.Holds;
using Ohmline.Testing;

namespace Ohmline.ContractTests;

/// <summary>
/// Billing's side of the Customer/Supplier contract with Charging: every request and response
/// of the hold and receipt API, as golden JSON in the shape ASP.NET Core writes and reads it.
/// Charging's payment port depends on exactly these.
/// </summary>
public sealed class BillingContractsTests
{
    public static TheoryData<string> Dtos => new(Samples.BillingDtos.Keys);

    [Fact]
    public void Every_type_in_Billings_contracts_has_a_sample()
    {
        var types = typeof(PlaceHoldRequest).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && type.GetProperties().Length > 0);
        var sampled = Samples.BillingDtos.Values.Select(sample => sample.GetType())
            .Append(typeof(Ohmline.Billing.Contracts.Receipts.ReceiptLine));

        types.ShouldBe(sampled, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void A_DTO_is_written_as_its_golden_file_says(string name)
    {
        var sample = Samples.BillingDtos[name];

        Golden.Verify(
            $"billing/{name}",
            JsonSerializer.Serialize(sample, sample.GetType(), JsonSerializerOptions.Web));
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void The_golden_file_reads_back_as_the_DTO(string name)
    {
        var sample = Samples.BillingDtos[name];

        JsonSerializer.Deserialize(
                Golden.Read($"billing/{name}"), sample.GetType(), JsonSerializerOptions.Web)
            .ShouldBe(sample);
    }
}
