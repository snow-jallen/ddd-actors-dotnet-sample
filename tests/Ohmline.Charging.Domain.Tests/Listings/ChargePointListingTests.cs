using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Domain.Listings;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Listings;

/// <summary>
/// A listing is state sourced: every revision records the whole listing and the next revision
/// number, and the decider still checks the text and ignores a revision that changes nothing.
/// </summary>
public sealed class ChargePointListingTests
{
    private const string Name = "Salt Lake bus depot, public bays";
    private const string Access = "Level -2, bays 14-19";
    private const string Hours = "24/7";

    [Fact]
    public void Given_no_listing_when_revised_then_it_is_listed_at_revision_1() =>
        ListingSpec.Given().At(Morning)
            .When(Revise(Name, Access, Hours))
            .Then(new ChargePointListingRevised(Charger, Name, Access, Hours, 1).At(Morning));

    [Fact]
    public void Given_a_listing_when_revised_then_all_of_it_is_recorded_at_the_next_revision()
    {
        var later = Morning.AddDays(3);

        ListingSpec.Given(Revised(Name, Access, Hours, 1), Revised(Name, "Level -1", Hours, 2))
            .At(later)
            .When(Revise(Name, "Level -2, bays 14-19", "Mon-Fri 05:00-23:00"))
            .Then(new ChargePointListingRevised(
                Charger, Name, "Level -2, bays 14-19", "Mon-Fri 05:00-23:00", 3).At(later));
    }

    [Fact]
    public void Given_a_listing_when_revised_to_the_same_text_then_nothing_changes() =>
        ListingSpec.Given(Revised(Name, Access, Hours, 1)).At(Morning)
            .When(Revise(Name, Access, Hours))
            .ThenNothingChanges();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_a_blank_display_name_then_the_revision_is_refused(string blank) =>
        ListingSpec.Given().At(Morning)
            .When(Revise(blank, Access, Hours))
            .ThenRejects(new ListingError.DisplayNameMissing());

    [Fact]
    public void Given_text_longer_than_the_app_shows_then_the_revision_is_refused_naming_it()
    {
        var tooLong = new string('x', ChargePointListing.AccessInstructionsMaxLength + 1);

        ListingSpec.Given(Revised(Name, Access, Hours, 1)).At(Morning)
            .When(Revise(Name, tooLong, Hours))
            .ThenRejects(new ListingError.TextTooLong("access instructions", 501, 500));
    }

    [Theory]
    [InlineData(61, 1, 1, "display name")]
    [InlineData(1, 1, 201, "opening hours")]
    public void Given_any_field_too_long_then_the_refusal_names_that_field(
        int nameLength, int accessLength, int hoursLength, string field) =>
        ListingSpec.Given().At(Morning)
            .When(Revise(
                new string('x', nameLength),
                new string('x', accessLength),
                new string('x', hoursLength)))
            .ThenRejects<ListingError.TextTooLong>().Field.ShouldBe(field);

    [Fact]
    public void Given_any_listing_when_a_revision_is_applied_then_it_replaces_the_state()
    {
        ListingState before = new Listed(Charger, "Old name", "Old way in", "Never", 7);

        ChargePointListing.Evolve(before, Revised(Name, Access, Hours, 8))
            .ShouldBe(new Listed(Charger, Name, Access, Hours, 8));
    }

    private static ReviseChargePointListing Revise(string name, string access, string hours) =>
        new(Command, Charger, name, access, hours);

    private static ChargePointListingEvent Revised(
        string name, string access, string hours, long revision) =>
        new ChargePointListingRevised(Charger, name, access, hours, revision).At(Morning);
}
