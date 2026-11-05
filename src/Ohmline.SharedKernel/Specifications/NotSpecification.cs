namespace Ohmline.SharedKernel.Specifications;

/// <summary>Satisfied when the rule is not.</summary>
public sealed record NotSpecification<T>(ISpecification<T> Rule) : ISpecification<T>
{
    public bool IsSatisfiedBy(T candidate) => !Rule.IsSatisfiedBy(candidate);
}
