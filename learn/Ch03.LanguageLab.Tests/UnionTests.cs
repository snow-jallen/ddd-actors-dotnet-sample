using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ch03.LanguageLab.Tests;

public sealed class UnionTests
{
    [Fact]
    public void Given_a_case_value_when_assigned_to_the_union_then_it_converts_implicitly()
    {
        ConnectorStatus status = new Occupied("session-a");

        status.Value.ShouldBe(new Occupied("session-a"));
    }

    [Fact]
    public void Given_a_union_declaration_then_the_compiler_emits_a_struct_holding_an_object()
    {
        var type = typeof(ConnectorStatus);

        type.IsValueType.ShouldBeTrue();
        type.IsAssignableTo(typeof(IUnion)).ShouldBeTrue();
        type.GetCustomAttribute<UnionAttribute>().ShouldNotBeNull();
        type.GetProperty(nameof(ConnectorStatus.Value))!.PropertyType.ShouldBe(typeof(object));
        type.GetConstructors()
            .Select(constructor => constructor.GetParameters().Single().ParameterType)
            .ShouldBe([typeof(Available), typeof(Occupied), typeof(Faulted)]);
    }

    [Fact]
    public void Given_each_status_when_described_then_the_switch_picks_its_arm()
    {
        Describe(new Available()).ShouldBe("available");
        Describe(new Occupied("session-a")).ShouldBe("occupied by session-a");
        Describe(new Faulted("E-42")).ShouldBe("faulted: E-42");
    }

    [Fact]
    public void Given_a_default_union_then_it_holds_no_case()
    {
        var status = default(ConnectorStatus);

        status.Value.ShouldBeNull();
        (status is null).ShouldBeTrue();
    }

    [Fact]
    public void Given_a_default_union_when_the_switch_has_no_null_arm_then_it_throws()
    {
        Should.Throw<SwitchExpressionException>(() => DescribeWithoutNullArm(default));
    }

    [Fact]
    public void Given_a_default_union_when_the_switch_has_a_null_arm_then_it_is_handled()
    {
        Describe(default).ShouldBe("unknown");
    }

    [Fact]
    public void Given_a_nested_union_when_converting_a_case_then_one_explicit_hop_is_needed()
    {
        StatusReport report = (ConnectorStatus)new Occupied("session-a");

        report.Value.ShouldBeOfType<ConnectorStatus>().Value.ShouldBe(new Occupied("session-a"));
    }

    [Fact]
    public void Given_a_nested_union_when_matching_then_the_pattern_names_the_inner_union()
    {
        StatusReport report = (ConnectorStatus)new Occupied("session-a");

        (report is ConnectorStatus and Occupied { SessionId: "session-a" }).ShouldBeTrue();
        (report is ChargerOffline).ShouldBeFalse();
    }

    [Fact]
    public void Given_a_union_when_printed_then_only_its_type_name_is_shown()
    {
        ConnectorStatus status = new Occupied("session-a");

        status.ToString().ShouldBe("Ch03.LanguageLab.Tests.ConnectorStatus");
        status.Value!.ToString().ShouldBe("Occupied { SessionId = session-a }");
    }

    [Fact]
    public void Given_two_unions_holding_equal_records_then_they_are_equal()
    {
        ConnectorStatus first = new Faulted("E-42");
        ConnectorStatus second = new Faulted("E-42");

        first.ShouldBe(second);
    }

    private static string Describe(ConnectorStatus status) => status switch
    {
        Available => "available",
        Occupied occupied => $"occupied by {occupied.SessionId}",
        Faulted faulted => $"faulted: {faulted.ErrorCode}",
        null => "unknown",
    };

    // No warning: every case type is non-nullable, so the compiler does not expect null here.
    private static string DescribeWithoutNullArm(ConnectorStatus status) => status switch
    {
        Available => "available",
        Occupied occupied => $"occupied by {occupied.SessionId}",
        Faulted faulted => $"faulted: {faulted.ErrorCode}",
    };
}
