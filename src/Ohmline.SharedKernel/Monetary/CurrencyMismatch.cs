namespace Ohmline.SharedKernel.Monetary;

/// <summary>Two amounts in different currencies met. They are never added or compared.</summary>
public sealed record CurrencyMismatch(Currency Left, Currency Right)
{
    public string Message => $"Cannot combine {Left} with {Right}: convert one of them first.";
}
