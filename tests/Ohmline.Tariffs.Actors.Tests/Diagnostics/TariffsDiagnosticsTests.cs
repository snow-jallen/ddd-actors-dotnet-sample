using Ohmline.Tariffs.Actors.Diagnostics;

namespace Ohmline.Tariffs.Actors.Tests.Diagnostics;

public sealed class TariffsDiagnosticsTests
{
    [Fact]
    public void Tariffs_spans_and_instruments_are_recorded_under_one_name()
    {
        TariffsDiagnostics.ActivitySource.Name.ShouldBe("Ohmline.Tariffs");
        TariffsDiagnostics.MeterName.ShouldBe("Ohmline.Tariffs");
    }
}
