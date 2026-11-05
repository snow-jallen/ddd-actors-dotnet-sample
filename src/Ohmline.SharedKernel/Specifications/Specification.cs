namespace Ohmline.SharedKernel.Specifications;

/// <summary>
/// Combining rules reads like the sentence a domain expert would say:
/// <c>new IdleFeeApplies(soon).And(new IdleFeeApplies(now).Not())</c>. Extension members, so
/// every specification gets them without a base class.
/// </summary>
public static class Specification
{
    extension<T>(ISpecification<T> specification)
    {
        public ISpecification<T> And(ISpecification<T> other) =>
            new AndSpecification<T>(specification, other);

        public ISpecification<T> Or(ISpecification<T> other) =>
            new OrSpecification<T>(specification, other);

        public ISpecification<T> Not() => new NotSpecification<T>(specification);
    }
}
