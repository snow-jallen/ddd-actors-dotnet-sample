#if DEMO_COMPILE_ERRORS
namespace Ohmline.SharedKernel.Tests;

// None of this compiles. To see the errors:
//   dotnet build tests/Ohmline.SharedKernel.Tests -p:DefineConstants=DEMO_COMPILE_ERRORS
public static class ValueObjectCompileErrors
{
    // The default implementation of a static virtual member is not a member of the type
    // that implements the interface.
    public static StateOfCharge CreateThroughTheType() => StateOfCharge.Create(80);
}
#endif
