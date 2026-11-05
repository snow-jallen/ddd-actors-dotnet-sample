using System.Text.RegularExpressions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Tests;

public sealed partial class ErrorMappingTests
{
    private static readonly ChargePointId ChargePoint = new("OHM-SLC-0042");

    private static readonly Dictionary<string, ChargePointError> Reasons = new()
    {
        [nameof(ChargePointError.NotRegistered)] = new ChargePointError.NotRegistered(ChargePoint),
        [nameof(ChargePointError.AlreadyRegistered)] =
            new ChargePointError.AlreadyRegistered(ChargePoint),
        [nameof(ChargePointError.UnknownConnector)] = new ChargePointError.UnknownConnector(3),
        [nameof(ChargePointError.ConnectorUnavailable)] =
            new ChargePointError.ConnectorUnavailable(1),
        [nameof(ChargePointError.ReservedForAnotherToken)] =
            new ChargePointError.ReservedForAnotherToken(
                1, new DateTimeOffset(2026, 11, 4, 7, 55, 0, TimeSpan.Zero)),
        [nameof(ChargePointError.ConnectorFaulted)] =
            new ChargePointError.ConnectorFaulted(2, "GroundFailure"),
    };

    public static readonly TheoryData<string, string> Codes = new()
    {
        { nameof(ChargePointError.NotRegistered), "not-registered" },
        { nameof(ChargePointError.AlreadyRegistered), "already-registered" },
        { nameof(ChargePointError.UnknownConnector), "unknown-connector" },
        { nameof(ChargePointError.ConnectorUnavailable), "connector-unavailable" },
        { nameof(ChargePointError.ReservedForAnotherToken), "reserved-for-another-token" },
        { nameof(ChargePointError.ConnectorFaulted), "connector-faulted" },
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void Given_a_reason_when_mapped_then_the_reply_carries_its_code_and_message(
        string reason, string code)
    {
        var error = Reasons[reason];

        error.ToReply().ShouldBe(new CommandRejected(code, error.Message));
    }

    [Fact]
    public void Every_reason_a_charge_point_can_give_has_a_row()
    {
        var cases = typeof(ChargePointError).GetConstructors()
            .Select(constructor => constructor.GetParameters().Single().ParameterType.Name);

        Reasons.Keys.ShouldBe(cases, ignoreOrder: true);
    }

    [Fact]
    public void Every_code_is_distinct_and_written_in_kebab_case()
    {
        var codes = Reasons.Values.Select(error => error.Code).ToList();

        codes.ShouldBeUnique();
        codes.ShouldAllBe(code => KebabCase().IsMatch(code));
    }

    [Fact]
    public void Given_a_default_reason_when_mapped_then_it_throws()
    {
        Should.Throw<InvalidOperationException>(() => default(ChargePointError).ToReply());
    }

    [GeneratedRegex("^[a-z]+(-[a-z]+)*$")]
    private static partial Regex KebabCase();
}
