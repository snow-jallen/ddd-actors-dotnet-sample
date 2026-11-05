namespace Ohmline.Charging.Domain.Listings;

/// <summary>Why a listing was not revised.</summary>
public union ListingError(ListingError.DisplayNameMissing, ListingError.TextTooLong)
{
    /// <summary>A sentence for logs and people. Code decides by the case, never by this.</summary>
    public string Message => this switch
    {
        DisplayNameMissing => "A listing needs a display name.",
        TextTooLong error =>
            $"The {error.Field} is {error.Length} characters long; at most {error.MaxLength} fit.",
        null => "This ListingError is default: it holds no case.",
    };

    /// <summary>Drivers find a charge point by its name: it cannot be blank.</summary>
    public sealed record DisplayNameMissing;

    /// <summary>The app has room for this much text, and no more.</summary>
    public sealed record TextTooLong(string Field, int Length, int MaxLength);
}
