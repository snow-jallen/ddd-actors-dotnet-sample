#if DEMO_COMPILE_ERRORS
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Domain.Tests.Sessions;

/// <summary>
/// Section 8.6: a switch that knew one suspended state, after <c>Suspended</c> became
/// <c>SuspendedByEv</c> and <c>SuspendedByCharger</c> and only one of them was added here. The
/// compiler names the state that is missing. Build with
/// <c>-p:DefineConstants=DEMO_COMPILE_ERRORS</c> to see it.
/// </summary>
internal static class SessionStateCompileErrors
{
    public static string Describe(SessionState state) => state switch
    {
        NoSession => "not started",
        Active => "charging",
        SuspendedByEv => "waiting for the driver",
        Ended => "ended",
        null => "unknown",
    };
}
#endif
