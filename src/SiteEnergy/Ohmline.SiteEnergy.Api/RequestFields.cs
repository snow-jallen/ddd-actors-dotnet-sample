using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.SharedKernel;

namespace Ohmline.SiteEnergy.Api;

/// <summary>
/// Reads a request's fields into value objects with their smart constructors and remembers
/// every field that is not valid, so that the client hears about all of them in one answer.
/// </summary>
internal sealed class RequestFields
{
    private readonly Dictionary<string, string[]> _errors = [];

    public bool AreValid => _errors.Count == 0;

    /// <summary>The value; or, when it is not valid, a default that must not be used.</summary>
    public T Read<T>(string field, Result<T, ValidationError> result)
    {
        if (result.TryGet(out var value, out var error))
        {
            return value;
        }

        Invalid(field, error.Message);
        return default!;
    }

    /// <summary>Records a problem no smart constructor can find, such as an empty list.</summary>
    public void Invalid(string field, string message) => _errors[field] = [message];

    public ProblemHttpResult Problem() => Problems.Invalid(_errors);
}
