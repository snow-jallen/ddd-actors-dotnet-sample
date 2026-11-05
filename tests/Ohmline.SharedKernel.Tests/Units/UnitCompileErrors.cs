#if DEMO_COMPILE_ERRORS
using Ohmline.SharedKernel.Units;

namespace Ohmline.SharedKernel.Tests.Units;

// None of this compiles. To see the errors:
//   dotnet build tests/Ohmline.SharedKernel.Tests -p:DefineConstants=DEMO_COMPILE_ERRORS
public static class UnitCompileErrors
{
    // CS0019: power and energy are different dimensions; there is no + between them.
    public static Energy PowerPlusEnergy(Power power, Energy energy) => power + energy;

    // CS0029: power times time is energy, not power.
    public static Power PowerTimesTime(Power power, TimeSpan duration) => power * duration;

    // CS0019: a bare decimal is not a quantity; say which unit it is in.
    public static Energy Bare(decimal kilowattHours) => new Energy(1m) + kilowattHours;
}
#endif
