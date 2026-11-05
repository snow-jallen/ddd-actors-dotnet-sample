namespace Ohmline.SharedKernel;

/// <summary>
/// The contract of every value object: it wraps one raw value, and TryCreate either returns a
/// valid instance or says why the raw value is invalid, without throwing.
/// </summary>
public interface IValueObject<TSelf, TValue>
    where TSelf : IValueObject<TSelf, TValue>
{
    TValue Value { get; }

    static abstract Result<TSelf, ValidationError> TryCreate(TValue value);

    /// <summary>
    /// For values that are valid by construction, such as constants and test data. An invalid
    /// value here is a bug, so it throws.
    /// </summary>
    static virtual TSelf Create(TValue value) => TSelf.TryCreate(value) switch
    {
        Ok<TSelf> ok => ok.Value,
        Err<ValidationError> err => throw new ArgumentException(err.Error.Message, nameof(value)),
        null => throw new InvalidOperationException($"{typeof(TSelf)}.TryCreate returned default."),
    };
}
