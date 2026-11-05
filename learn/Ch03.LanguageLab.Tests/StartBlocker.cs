using System.Text.Json.Serialization;

namespace Ch03.LanguageLab.Tests;

/// <summary>
/// Why a connector cannot start a session. Each case has a property the other lacks, which is
/// what the structural classifier needs.
/// </summary>
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union StartBlocker(Occupied, Faulted);
