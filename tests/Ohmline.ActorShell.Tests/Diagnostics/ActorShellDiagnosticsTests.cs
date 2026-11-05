using Ohmline.ActorShell.Diagnostics;

namespace Ohmline.ActorShell.Tests.Diagnostics;

public sealed class ActorShellDiagnosticsTests
{
    [Fact]
    public void Actor_shell_spans_and_instruments_are_recorded_under_one_name()
    {
        ActorShellDiagnostics.ActivitySource.Name.ShouldBe("Ohmline.ActorShell");
        ActorShellDiagnostics.MeterName.ShouldBe("Ohmline.ActorShell");
    }
}
