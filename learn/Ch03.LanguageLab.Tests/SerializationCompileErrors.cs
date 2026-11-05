#if DEMO_COMPILE_ERRORS
using System.Text.Json.Serialization;

namespace Ch03.LanguageLab.Tests;

// None of this compiles with TreatWarningsAsErrors. To see the diagnostic:
//   dotnet build learn/Ch03.LanguageLab.Tests -p:DefineConstants=DEMO_COMPILE_ERRORS

// SYSLIB1227: the source generator sees three cases that all serialize as a JSON object.
[JsonSerializable(typeof(ConnectorStatus))]
public sealed partial class ConnectorStatusJsonContext : JsonSerializerContext;
#endif
