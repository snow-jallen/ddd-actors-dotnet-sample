namespace Ohmline.SharedKernel.Specifications;

/// <summary>Satisfied when either rule is. The right one is asked only if the left fails.</summary>
public sealed record OrSpecification<T>(ISpecification<T> Left, ISpecification<T> Right)
    : ISpecification<T>
{
    public bool IsSatisfiedBy(T candidate) =>
        Left.IsSatisfiedBy(candidate) || Right.IsSatisfiedBy(candidate);
}
