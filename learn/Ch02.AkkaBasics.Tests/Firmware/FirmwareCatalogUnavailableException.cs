namespace Ch02.AkkaBasics.Tests.Firmware;

public sealed class FirmwareCatalogUnavailableException(string model, Exception innerException)
    : Exception($"The firmware catalog could not be reached for model {model}.", innerException);
