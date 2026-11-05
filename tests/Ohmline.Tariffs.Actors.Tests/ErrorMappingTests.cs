using Ohmline.SharedKernel;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;
using static Ohmline.Tariffs.Actors.Tests.TheBook;

namespace Ohmline.Tariffs.Actors.Tests;

public sealed class ErrorMappingTests
{
    private static readonly Dictionary<string, TariffBookError> Reasons = new()
    {
        [nameof(ValidationError)] = Result.Fail<ValidationError>(new Missing("Time zone")).Error,
        [nameof(TariffBookError.NotPublished)] = new TariffBookError.NotPublished(Standard),
    };

    public static readonly TheoryData<string, string> Codes = new()
    {
        { nameof(ValidationError), "invalid-tariff" },
        { nameof(TariffBookError.NotPublished), "not-published" },
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void Given_a_reason_when_mapped_then_the_reply_carries_its_code_and_message(
        string reason, string code)
    {
        var error = Reasons[reason];

        error.ToReply().ShouldBe(new TariffBookRejected(code, error.Message));
    }

    [Fact]
    public void Every_reason_the_book_can_give_has_a_row() =>
        Reasons.Keys.ShouldBe(
            typeof(TariffBookError).GetConstructors()
                .Select(constructor => constructor.GetParameters().Single().ParameterType.Name),
            ignoreOrder: true);
}
