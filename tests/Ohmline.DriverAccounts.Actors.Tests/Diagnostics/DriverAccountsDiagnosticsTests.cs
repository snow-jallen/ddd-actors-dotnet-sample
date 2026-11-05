using Ohmline.DriverAccounts.Actors.Diagnostics;

namespace Ohmline.DriverAccounts.Actors.Tests.Diagnostics;

public sealed class DriverAccountsDiagnosticsTests
{
    [Fact]
    public void Driver_Accounts_spans_and_instruments_are_recorded_under_one_name()
    {
        DriverAccountsDiagnostics.ActivitySource.Name.ShouldBe("Ohmline.DriverAccounts");
        DriverAccountsDiagnostics.MeterName.ShouldBe("Ohmline.DriverAccounts");
    }
}
