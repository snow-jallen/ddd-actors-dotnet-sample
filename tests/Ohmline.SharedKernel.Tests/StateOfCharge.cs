namespace Ohmline.SharedKernel.Tests;

/// <summary>A car's battery level in per cent: a small value object to test the contract.</summary>
public sealed record StateOfCharge : IValueObject<StateOfCharge, int>
{
    private StateOfCharge(int value) => Value = value;

    public int Value { get; }

    public static Result<StateOfCharge, ValidationError> TryCreate(int value) =>
        value is >= 0 and <= 100
            ? Result.Ok(new StateOfCharge(value))
            : Result.Fail<ValidationError>(new OutOfRange("State of charge", value, "0 to 100 %"));
}
