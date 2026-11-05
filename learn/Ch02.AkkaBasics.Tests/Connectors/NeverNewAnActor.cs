#if DEMO_COMPILE_ERRORS
namespace Ch02.AkkaBasics.Tests.Connectors;

// Compile with -p:DefineConstants=DEMO_COMPILE_ERRORS to see Akka.Analyzers reject this (AK1000).
public static class NeverNewAnActor
{
    public static AkkaConnectorActor Create() => new AkkaConnectorActor("OHM-SLC-0042/1");
}
#endif
