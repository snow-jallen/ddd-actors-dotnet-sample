using Ohmline.SharedKernel;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;

namespace Ohmline.Tariffs.Actors;

/// <summary>
/// From the book's reasons to Tariffs' wire reply, with an exhaustive switch. Codes are
/// published vocabulary: add new ones, never change or reuse one.
/// </summary>
public static class ErrorMapping
{
    extension(TariffBookError error)
    {
        public string Code => error switch
        {
            ValidationError => "invalid-tariff",
            TariffBookError.NotPublished => "not-published",
            null => throw new InvalidOperationException(
                "This TariffBookError is default: it holds no case to report."),
        };

        public TariffBookRejected ToReply() => new(error.Code, error.Message);
    }
}
