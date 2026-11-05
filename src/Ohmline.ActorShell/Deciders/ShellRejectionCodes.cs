namespace Ohmline.ActorShell.Deciders;

/// <summary>
/// Rejection codes that come from the shell, not from a decider: the command may have been
/// right, but the shell could not keep the decision. Stable, like the deciders' own codes.
/// </summary>
public static class ShellRejectionCodes
{
    /// <summary>
    /// The journal refused the decision's events (section 11.6). Nothing changed.
    /// </summary>
    public const string PersistenceRejected = "persistence-rejected";
}
