namespace Ohmline.SharedKernel;

/// <summary>
/// The one value of a type that carries no information. <c>Result&lt;Unit, E&gt;</c> is an
/// operation that succeeds with nothing to return, or fails with an E.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;

    public override string ToString() => "()";
}
