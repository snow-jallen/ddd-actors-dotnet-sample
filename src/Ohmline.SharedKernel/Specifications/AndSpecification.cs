namespace Ohmline.SharedKernel.Specifications;

/// <summary>Satisfied when both rules are. The right one is asked only if the left holds.</summary>
public sealed record AndSpecification<T>(ISpecification<T> Left, ISpecification<T> Right)
    : ISpecification<T>
{
    public bool IsSatisfiedBy(T candidate) =>
        Left.IsSatisfiedBy(candidate) && Right.IsSatisfiedBy(candidate);
}
