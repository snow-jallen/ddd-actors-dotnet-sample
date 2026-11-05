#if DEMO_COMPILE_ERRORS
using Ch03.ClosedLib;

namespace Ch03.LanguageLab.Tests;

// None of this compiles. To see the errors in method bodies:
//   dotnet build learn/Ch03.LanguageLab.Tests -p:DefineConstants=DEMO_COMPILE_ERRORS
public static class ClosedHierarchyCompileErrors
{
    // CS8509: without a discard arm, a plain abstract record is never exhaustive.
    public static string NoDiscard(ChargerNotification notification) => notification switch
    {
        BootNotification => "booted",
        Heartbeat => "alive",
    };
}

// Once a type declaration is in error, the compiler reports nothing from method bodies, so
// these three need a second symbol:
//   dotnet build learn/Ch03.LanguageLab.Tests \
//     -p:DefineConstants=DEMO_COMPILE_ERRORS%3BDECLARATION_ERRORS
#if DECLARATION_ERRORS

// CS9382: a closed type from another assembly cannot be a base type.
public sealed record ResetCharger(string ChargePointId) : ChargerCommand(ChargePointId);

// CS9381: a closed type cannot be sealed.
public closed sealed record SealedAndClosed;

// CS9384: a closed type is already abstract.
public abstract closed record AbstractAndClosed;

#endif
#endif
