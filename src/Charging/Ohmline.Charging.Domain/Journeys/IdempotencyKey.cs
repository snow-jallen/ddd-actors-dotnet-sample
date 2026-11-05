using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// What makes a request safe to send twice (section 15.6): a key derived from the journey and
/// the step, such as <c>{journeyId}-hold</c>. A journey that sends a request again, after a
/// lost answer, a restart or a rebalance, sends it under the same key, and its receiver does
/// what it did the first time instead of doing it again.
/// </summary>
public readonly record struct IdempotencyKey
{
    public const string Lock = "lock";
    public const string Hold = "hold";
    public const string Allocation = "allocation";
    public const string Charger = "charger";
    public const string Release = "release";
    public const string Capture = "capture";
    public const string Unlock = "unlock";

    private IdempotencyKey(JourneyId journeyId, string step) =>
        (JourneyId, Step) = (journeyId, step);

    public JourneyId JourneyId { get; }

    public string Step { get; }

    /// <summary>The key as the payment side and the charge points see it.</summary>
    public string Value => $"{JourneyId}-{Step}";

    /// <summary>
    /// The key as Driver Accounts and Site Energy see it: the id of the command, the same every
    /// time, so their shells answer a command sent again with the first answer (section 12.6).
    /// </summary>
    public CommandId CommandId => new(GuidV7.Derive(JourneyId.Value, Step));

    public static IdempotencyKey Of(JourneyId journeyId, string step) => new(journeyId, step);

    public override string ToString() => Value;
}
