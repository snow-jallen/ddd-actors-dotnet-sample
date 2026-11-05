using System.Text.Json.Serialization;

namespace Ch03.LanguageLab.Tests;

/// <summary>
/// ConnectorStatus with a classifier that picks the case from the JSON property names. Available
/// has no properties, so the classifier can never pick it, and refuses the whole union.
/// </summary>
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union StructuralConnectorStatus(Available, Occupied, Faulted);
