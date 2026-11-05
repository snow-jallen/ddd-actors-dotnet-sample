#if DEMO_COMPILE_ERRORS
namespace Ohmline.SharedKernel.Tests;

// None of this compiles. To see the errors:
//   dotnet build tests/Ohmline.SharedKernel.Tests -p:DefineConstants=DEMO_COMPILE_ERRORS
public static class ResultCompileErrors
{
    // CS0029: ConnectorBusy converts to StartError, and Err<StartError> converts to Result,
    // but the compiler never chains the two conversions.
    public static Result<string, StartError> TwoHops() => new ConnectorBusy(1);

    // CS0029: Fail infers E = ConnectorBusy, and Err<ConnectorBusy> is not a case of this Result.
    public static Result<string, StartError> WrongErrorType() => Result.Fail(new ConnectorBusy(1));
}
#endif
