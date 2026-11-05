namespace Ch02.AkkaBasics.Tests.Chargers;

public enum ChargerStatus
{
    Available,
    Charging,
    Faulted,

    /// <summary>Never reported by a charger: the twin has stopped hearing from it.</summary>
    Offline,
}
