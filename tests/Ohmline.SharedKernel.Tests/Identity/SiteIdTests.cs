using Ohmline.SharedKernel.Identity;

namespace Ohmline.SharedKernel.Tests.Identity;

public sealed class SiteIdTests
{
    [Theory]
    [InlineData("slc-depot", "slc-depot")]
    [InlineData(" SLC-West-Garage ", "slc-west-garage")]
    [InlineData("ogd-library", "ogd-library")]
    public void Given_a_site_code_when_trying_to_create_then_it_is_kebab_case(
        string text, string expected)
    {
        SiteId.TryCreate(text).ShouldBe(Result.Ok(new SiteId(expected)));
    }

    [Theory]
    [InlineData("slc_depot")]
    [InlineData("slc depot")]
    [InlineData("depot")]
    [InlineData("sl-depot")]
    [InlineData("slc-")]
    public void Given_text_that_is_no_site_id_when_trying_to_create_then_it_is_malformed(
        string text)
    {
        SiteId.TryCreate(text).ShouldBe(Result.Fail<ValidationError>(new Malformed(
            "SiteId", text, "an area code and hyphenated words, like slc-depot")));
    }

    [Fact]
    public void Given_no_text_when_trying_to_create_then_it_is_missing()
    {
        SiteId.TryCreate("").ShouldBe(Result.Fail<ValidationError>(new Missing("SiteId")));
    }

    [Fact]
    public void Given_a_site_id_that_is_not_canonical_when_constructed_then_the_guard_throws()
    {
        Should.Throw<ArgumentException>(() => new SiteId("SLC-DEPOT"));
    }
}
