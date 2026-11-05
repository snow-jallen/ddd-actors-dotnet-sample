namespace Ohmline.Migrator;

/// <summary>
/// One of Ohmline's databases, as the migrator sees it: the connection string it is found by,
/// the folder of scripts that make its schema, and the role whose rights it keeps up to date.
/// </summary>
/// <param name="ConnectionName">The key under <c>ConnectionStrings</c>.</param>
/// <param name="Scripts">The folder under <c>Scripts/</c>; each script in it runs once.</param>
/// <param name="Grants">The script under <c>Grants/</c> that runs after every migration.</param>
public sealed record MigrationTarget(string ConnectionName, string Scripts, string Grants)
{
    /// <summary>The journal, snapshots, tags and read models: what every node shares.</summary>
    public static MigrationTarget Ohmline { get; } = new("ohmline", "Ohmline", "ohmline.sql");

    /// <summary>Billing's own database (section 16.3).</summary>
    public static MigrationTarget Billing { get; } = new("billing", "Billing", "billing.sql");

    public static IReadOnlyList<MigrationTarget> All { get; } = [Ohmline, Billing];
}
