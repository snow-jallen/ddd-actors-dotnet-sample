namespace Ohmline.SharedKernel.Specifications;

/// <summary>
/// A business rule that a candidate satisfies or not, as a value: it can be named, passed to a
/// projection or a router, logged, and combined with <c>And</c>, <c>Or</c> and <c>Not</c>
/// (Evans, chapter 9). Contravariant: a rule about every session event holds for each kind.
/// </summary>
public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T candidate);
}
