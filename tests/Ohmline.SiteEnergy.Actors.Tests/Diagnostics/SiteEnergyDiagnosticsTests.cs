using Ohmline.SiteEnergy.Actors.Diagnostics;

namespace Ohmline.SiteEnergy.Actors.Tests.Diagnostics;

public sealed class SiteEnergyDiagnosticsTests
{
    [Fact]
    public void Site_Energy_spans_and_instruments_are_recorded_under_one_name()
    {
        SiteEnergyDiagnostics.ActivitySource.Name.ShouldBe("Ohmline.SiteEnergy");
        SiteEnergyDiagnostics.MeterName.ShouldBe("Ohmline.SiteEnergy");
    }
}
