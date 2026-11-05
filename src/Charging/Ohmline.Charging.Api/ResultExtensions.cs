using Ohmline.SharedKernel;

namespace Ohmline.Charging.Api;

/// <summary>Reading a smart constructor's result where an <c>if</c> reads best.</summary>
internal static class ResultExtensions
{
    extension<T>(Result<T, ValidationError> result)
    {
        public bool TryGet(out T value) => result.TryGet(out value, out _);

        public bool TryGet(out T value, out ValidationError error)
        {
            (var ok, value, error) = result switch
            {
                Ok<T> valid => (true, valid.Value, default(ValidationError)),
                Err<ValidationError> invalid => (false, default!, invalid.Error),
                null => throw new InvalidOperationException("This Result is default."),
            };
            return ok;
        }
    }
}
