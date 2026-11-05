using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Acl.GridOperator;

/// <summary>
/// What Northgate wants of one site right now, in Site Energy's words: draw this much less, or
/// draw as usual. Each names the Northgate event revision it comes from, which is what makes a
/// command sent for it the same command every time.
/// </summary>
public union GridInstruction(Curtail, Restore)
{
    public SiteId SiteId => this switch
    {
        Curtail curtail => curtail.SiteId,
        Restore restore => restore.SiteId,
        null => throw new InvalidOperationException("The instruction is default: no case."),
    };
}

public sealed record Curtail(SiteId SiteId, Power Reduction, string EventId, int Modification);

public sealed record Restore(SiteId SiteId, string EventId, int Modification);
