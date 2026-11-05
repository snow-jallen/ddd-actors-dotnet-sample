using System.Globalization;

namespace Ohmline.JournalInspect;

/// <summary>
/// One row as one line of <c>column=value</c> pairs, as the journal tour prints them. A message
/// that came in a trace envelope shows both layers, the envelope's first, such as
/// <c>serializer_id=9100&gt;9101</c>.
/// </summary>
public static class RowFormat
{
    public static string Line(StoredRow row)
    {
        var payload = StoredPayload.Read(row.SerializerId, row.Manifest, row.Payload);
        string?[] columns =
        [
            row.Ordering is { } ordering ? $"ordering={ordering}" : null,
            $"persistence_id={row.PersistenceId}",
            $"sequence_nr={row.SequenceNr}",
            $"created={row.CreatedAt.ToString("O", CultureInfo.InvariantCulture)}",
            $"serializer_id={Layers(payload, layer => Show(layer.SerializerId))}",
            $"manifest={Layers(payload, layer => layer.Manifest ?? "null")}",
            payload.TraceParent is { } trace ? $"traceparent={trace}" : null,
            $"payload={payload.Text}",
        ];
        return string.Join(" | ", columns.OfType<string>());
    }

    private static string Layers(StoredPayload payload, Func<StoredPayload, string> show) =>
        payload.Envelope is { } envelope ? $"{show(envelope)}>{show(payload)}" : show(payload);

    private static string Show(int? serializerId) =>
        serializerId?.ToString(CultureInfo.InvariantCulture) ?? "null";
}
