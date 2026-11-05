using Ohmline.SharedKernel.Specifications;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SharedKernel.Tests.Specifications;

public sealed class SpecificationTests
{
    private static readonly ISpecification<Energy> Some = new AtLeast(1m);
    private static readonly ISpecification<Energy> Plenty = new AtLeast(20m);

    [Theory]
    [InlineData(0, false, false, true)]
    [InlineData(5, false, true, false)]
    [InlineData(25, true, true, false)]
    public void Given_two_rules_then_and_or_and_not_combine_them_as_the_words_say(
        decimal kilowattHours, bool and, bool or, bool not)
    {
        var energy = new Energy(kilowattHours);

        Some.And(Plenty).IsSatisfiedBy(energy).ShouldBe(and);
        Some.Or(Plenty).IsSatisfiedBy(energy).ShouldBe(or);
        Some.Not().IsSatisfiedBy(energy).ShouldBe(not);
    }

    [Fact]
    public void Given_a_combination_that_is_decided_by_its_left_rule_then_the_right_is_not_asked()
    {
        var never = new Unaskable();

        Plenty.And(never).IsSatisfiedBy(new Energy(5m)).ShouldBeFalse();
        Some.Or(never).IsSatisfiedBy(new Energy(5m)).ShouldBeTrue();
    }

    [Fact]
    public void Given_combinations_built_from_equal_rules_then_they_are_equal_values()
    {
        Some.And(Plenty.Not()).ShouldBe(new AtLeast(1m).And(new AtLeast(20m).Not()));
        Some.And(Plenty.Not()).ToString().ShouldBe(
            "AndSpecification { Left = AtLeast { KilowattHours = 1 }, "
            + "Right = NotSpecification { Rule = AtLeast { KilowattHours = 20 } } }");
    }

    [Fact]
    public void Given_a_rule_about_any_object_then_it_is_a_rule_about_strings()
    {
        ISpecification<string> named = new NotNullOrEmpty();

        named.And(new NotNullOrEmpty()).IsSatisfiedBy("slc-depot").ShouldBeTrue();
    }

    private sealed record AtLeast(decimal KilowattHours) : ISpecification<Energy>
    {
        public bool IsSatisfiedBy(Energy candidate) => candidate.KilowattHours >= KilowattHours;
    }

    private sealed record NotNullOrEmpty : ISpecification<object>
    {
        public bool IsSatisfiedBy(object candidate) => candidate is not (null or "");
    }

    private sealed record Unaskable : ISpecification<Energy>
    {
        public bool IsSatisfiedBy(Energy candidate) =>
            throw new InvalidOperationException("The combination asked a rule it did not need.");
    }
}
