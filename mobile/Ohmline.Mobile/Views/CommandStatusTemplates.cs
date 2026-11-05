using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Views;

/// <summary>
/// A command's status, one template per case (section 19.5), checked for every case of
/// <see cref="CommandStatus"/> by the compiler.
/// </summary>
public sealed class CommandStatusTemplates : IStateTemplates
{
    public DataTemplate Queued { get; set; } = null!;

    public DataTemplate Sending { get; set; } = null!;

    public DataTemplate Accepted { get; set; } = null!;

    public DataTemplate Rejected { get; set; } = null!;

    public DataTemplate FailedToSync { get; set; } = null!;

    public (DataTemplate Template, object Case) Select(object state) =>
        (CommandStatus)state switch
        {
            Queued queued => (Queued, queued),
            Sending sending => (Sending, sending),
            Accepted accepted => (Accepted, accepted),
            Rejected rejected => (Rejected, rejected),
            FailedToSync failed => (FailedToSync, failed),
            null => throw new InvalidOperationException("The command status is default."),
        };
}
