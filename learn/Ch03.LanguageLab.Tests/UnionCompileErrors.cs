#if DEMO_COMPILE_ERRORS
namespace Ch03.LanguageLab.Tests;

// None of this compiles. To see the errors:
//   dotnet build learn/Ch03.LanguageLab.Tests -p:DefineConstants=DEMO_COMPILE_ERRORS
public static class UnionCompileErrors
{
    // CS0029: conversions do not chain from Occupied through ConnectorStatus to StatusReport.
    public static StatusReport ConversionsDoNotChain() => new Occupied("session-a");

    // CS8121: patterns on a StatusReport do not reach into the nested ConnectorStatus.
    public static bool PatternsDoNotReachNestedUnions(StatusReport report) => report is Occupied;

    // CS8121: patterns test the union's contents, and no case is itself a ConnectorStatus.
    public static bool AUnionIsNotItsOwnCase(ConnectorStatus status) => status is ConnectorStatus;

    // CS8509: a forgotten case.
    public static string MissingCase(ConnectorStatus status) => status switch
    {
        Available => "available",
        Occupied => "occupied",
        null => "unknown",
    };

    // CS8655: a nullable input without a null arm.
    public static string MissingNull(Faulted? lastFault) => lastFault switch
    {
        Faulted fault => fault.ErrorCode,
    };
}
#endif
