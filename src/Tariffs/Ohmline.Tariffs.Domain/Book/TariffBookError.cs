using Ohmline.SharedKernel;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain.Book;

/// <summary>Why the tariff book refused a command.</summary>
public union TariffBookError(ValidationError, TariffBookError.NotPublished)
{
    /// <summary>A sentence for logs and people. Code decides by the case, never by this.</summary>
    public string Message => this switch
    {
        ValidationError error => $"The tariff is not valid: {error.Message}",
        NotPublished error => $"Tariff {error.TariffId} has never been published.",
        null => "This TariffBookError is default: it holds no case.",
    };

    public sealed record NotPublished(TariffId TariffId);
}
