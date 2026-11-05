namespace Ohmline.SiteEnergy.Domain.Sites;

/// <summary>
/// Who is served first when a site's capacity runs short: fleet requests before public ones.
/// A closed record, not a union or an enum: it is written into events (section 7.8), and no
/// other assembly can add a class that the allocator would not know.
/// </summary>
public closed record PriorityClass;

/// <summary>A fleet customer's vehicles, served first by contract.</summary>
public sealed record Fleet : PriorityClass;

/// <summary>Any other driver.</summary>
public sealed record Public : PriorityClass;
