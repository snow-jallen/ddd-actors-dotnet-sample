namespace Ohmline.SharedKernel;

/// <summary>
/// The outcome of an operation that can fail in an expected way: a value or an error. Return
/// it instead of throwing. Only a default Result holds neither, and every switch has a null arm.
/// </summary>
public union Result<T, E>(Ok<T>, Err<E>);

public sealed record Ok<T>(T Value);

public sealed record Err<E>(E Error);

public static class Result
{
    public static Ok<T> Ok<T>(T value) => new(value);

    public static Ok<Unit> Ok() => new(Unit.Value);

    /// <summary>
    /// Wraps an error. When E is itself a union, name it (<c>Fail&lt;StartError&gt;(...)</c>):
    /// union conversions do not chain, so the case needs one explicit hop.
    /// </summary>
    public static Err<E> Fail<E>(E error) => new(error);

    extension<T, E>(Result<T, E> result)
    {
        public bool IsOk => result is Ok<T>;

        public Result<TNext, E> Map<TNext>(Func<T, TNext> map) => result switch
        {
            Ok<T> ok => new Ok<TNext>(map(ok.Value)),
            Err<E> err => err,
            null => throw DefaultResult(),
        };

        public Result<TNext, E> Bind<TNext>(Func<T, Result<TNext, E>> next) => result switch
        {
            Ok<T> ok => next(ok.Value),
            Err<E> err => err,
            null => throw DefaultResult(),
        };

        public TOut Match<TOut>(Func<T, TOut> ok, Func<E, TOut> err) => result switch
        {
            Ok<T> success => ok(success.Value),
            Err<E> failure => err(failure.Error),
            null => throw DefaultResult(),
        };
    }

    private static InvalidOperationException DefaultResult() =>
        new("This Result is default: it holds neither Ok nor Err.");
}
