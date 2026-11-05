using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ohmline.Testing;

/// <summary>
/// Values as a failing test prints them. A union prints its case, not its own name; a record
/// prints its members, base members first, as the compiler's ToString does, except that a list
/// inside it prints its items rather than its type name; times print to the tick with their
/// offset.
/// </summary>
internal static class Show
{
    public static string Events<TEvent>(IEnumerable<TEvent> events) =>
        events.Any()
            ? string.Join(Environment.NewLine, events.Select(@event => $"    {Value(@event)}"))
            : "    nothing";

    /// <summary>The case a union holds, through unions of unions; others as they are.</summary>
    public static object? CaseOf(object? value) =>
        value is IUnion union ? CaseOf(union.Value) : value;

    public static string Value(object? value) => CaseOf(value) switch
    {
        null => "null",
        string text => $"\"{text}\"",
        DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
        IEnumerable items => $"[{string.Join(", ", items.Cast<object?>().Select(Value))}]",
        var record when IsRecordClass(record.GetType()) => Record(record),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString() ?? other.GetType().Name,
    };

    private static string Record(object record)
    {
        var members = Hierarchy(record.GetType())
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => $"{property.Name} = {Value(property.GetValue(record))}")
            .ToList();

        return members.Count == 0
            ? record.GetType().Name
            : $"{record.GetType().Name} {{ {string.Join(", ", members)} }}";
    }

    /// <summary>The record's base types first, then the record itself.</summary>
    private static IEnumerable<Type> Hierarchy(Type type) =>
        type.BaseType is { } parent && parent != typeof(object)
            ? [.. Hierarchy(parent), type]
            : [type];

    /// <summary>Record classes have a compiler-generated clone method; nothing else does.</summary>
    private static bool IsRecordClass(Type type) =>
        type.IsClass && type.GetMethod("<Clone>$") is not null;
}
