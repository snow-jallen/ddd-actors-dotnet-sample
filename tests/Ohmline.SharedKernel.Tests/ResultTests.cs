namespace Ohmline.SharedKernel.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Given_ok_when_mapped_then_the_value_is_transformed()
    {
        var result = ParseConnectorNumber("2").Map(number => $"connector {number}");

        result.ShouldBe(Result.Ok("connector 2"));
    }

    [Fact]
    public void Given_err_when_mapped_then_the_error_passes_through_untouched()
    {
        var mapped = false;

        var result = ParseConnectorNumber("two").Map(number => mapped = true);

        result.ShouldBe(Result.Fail("'two' is not a connector number."));
        mapped.ShouldBeFalse();
    }

    [Fact]
    public void Given_ok_when_bound_to_a_step_that_succeeds_then_both_steps_run()
    {
        var result = ParseConnectorNumber("2").Bind(number => OnChargePoint(number, 2));

        result.ShouldBe(Result.Ok(2));
    }

    [Fact]
    public void Given_ok_when_bound_to_a_step_that_fails_then_its_error_is_the_result()
    {
        var result = ParseConnectorNumber("3").Bind(number => OnChargePoint(number, 2));

        result.ShouldBe(Result.Fail("Connector 3 does not exist; the charge point has 2."));
    }

    [Fact]
    public void Given_err_when_bound_then_the_next_step_never_runs()
    {
        var calls = 0;

        var result = ParseConnectorNumber("").Bind(number =>
        {
            calls++;
            return OnChargePoint(number, 2);
        });

        result.IsOk.ShouldBeFalse();
        calls.ShouldBe(0);
    }

    [Fact]
    public void Given_either_outcome_when_matched_then_the_matching_function_runs()
    {
        static string Show(Result<int, string> result) =>
            result.Match(ok: number => $"connector {number}", err: error => $"rejected: {error}");

        Show(Result.Ok(1)).ShouldBe("connector 1");
        Show(Result.Fail("no such connector")).ShouldBe("rejected: no such connector");
    }

    [Fact]
    public void Given_an_error_union_when_failing_then_one_explicit_hop_converts_the_case()
    {
        var result = Start(connectorBusy: true);

        result.Match(ok: _ => "started", err: Explain).ShouldBe("connector 1 is busy");
    }

    [Fact]
    public void Given_nothing_to_return_when_successful_then_the_value_is_unit()
    {
        Result<Unit, string> result = Result.Ok();

        result.IsOk.ShouldBeTrue();
        result.Value.ShouldBe(new Ok<Unit>(Unit.Value));
        Unit.Value.ToString().ShouldBe("()");
    }

    [Fact]
    public void Given_a_default_result_when_used_then_it_throws_rather_than_guessing()
    {
        var result = default(Result<int, string>);

        result.IsOk.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => result.Map(number => number + 1));
    }

    private static Result<int, string> ParseConnectorNumber(string text) =>
        int.TryParse(text, out var number)
            ? Result.Ok(number)
            : Result.Fail($"'{text}' is not a connector number.");

    private static Result<int, string> OnChargePoint(int number, int count) =>
        number >= 1 && number <= count
            ? Result.Ok(number)
            : Result.Fail($"Connector {number} does not exist; the charge point has {count}.");

    private static Result<string, StartError> Start(bool connectorBusy) =>
        connectorBusy
            ? Result.Fail<StartError>(new ConnectorBusy(1))
            : Result.Ok("session-a");

    private static string Explain(StartError error) => error switch
    {
        ConnectorBusy busy => $"connector {busy.ConnectorNumber} is busy",
        TokenRejected rejected => $"token {rejected.TokenUid} was rejected",
        null => "no error",
    };
}
