using System.Numerics;

namespace Ch03.LanguageLab.Tests;

/// <summary>Algorithms written once against operator interfaces, for any quantity.</summary>
public static class GenericMath
{
    public static T Sum<T>(IEnumerable<T> values)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        var total = T.AdditiveIdentity;
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    public static T Clamp<T>(T value, T min, T max)
        where T : IComparisonOperators<T, T, bool> =>
        value < min ? min : value > max ? max : value;
}
