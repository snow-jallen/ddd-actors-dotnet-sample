#if DEMO_COMPILE_ERRORS
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

// None of this compiles. To see the errors:
//   dotnet build tests/Ohmline.Charging.Domain.Tests -p:DefineConstants=DEMO_COMPILE_ERRORS
public static class DecisionTableCompileErrors
{
    // CS8509: the table has no row for an unavailable connector when the car is unplugged, and
    // the compiler names the missing pair. A sixth connector state would fail the same way.
    public static string OnRelease(ConnectorState state, ConnectorCommand command) =>
        (state, command) switch
        {
            (Occupied, ReleaseConnector) => "the session leaves the connector",
            (Available or Reserved or OutOfOrder, ReleaseConnector) => "nothing changes",
            (_, not ReleaseConnector) => "another table",
            (null, _) => "a bug",
        };
}
#endif
