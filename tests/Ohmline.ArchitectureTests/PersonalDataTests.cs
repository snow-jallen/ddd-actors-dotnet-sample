using System.Reflection;
using System.Text.RegularExpressions;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// No personal data in what is kept for ever (section 20.10). An event is never deleted, so a
/// driver's name, e-mail address or phone number written into one could never be erased. Events
/// name a driver by <c>TokenUid</c> and the opaque <c>DriverRef</c> only; whatever identifies a
/// person lives where it can be deleted. This finds, by name, any property of an event, however
/// deep in its values, that looks like it holds one.
/// </summary>
public sealed partial class PersonalDataTests
{
    /// <summary>Names that are not about a person, each with the reason.</summary>
    private static readonly HashSet<(Type Type, string Property)> NotPersonal =
    [
        (typeof(TimeOfUseBand), nameof(TimeOfUseBand.Name)), // A tariff's band, such as "peak".
    ];

    public static TheoryData<string> Projects =>
        new(Repository.DomainProjects.Concat(Repository.ContractsProjects));

    [Theory]
    [MemberData(nameof(Projects))]
    public void No_event_holds_a_property_named_like_personal_data(string project)
    {
        var events = Repository.Load(project).GetTypes().Where(IsEvent);

        events.SelectMany(@event => PersonalIn(@event, @event.Name, [])).ShouldBeEmpty();
    }

    [Fact]
    public void Every_context_s_events_are_among_those_checked()
    {
        var bases = Projects.Select(row => row.Data)
            .SelectMany(project => Repository.Load(project).GetTypes())
            .Where(IsEvent)
            .Select(@event => Ancestry(@event).Last(IsEventBase).Name)
            .ToHashSet();

        bases.ShouldBe(
            [
                "ChargePointEvent",
                "ChargePointListingEvent",
                "ChargingIntegrationEvent",
                "IdTokenEvent",
                "JourneyEvent",
                "SessionEvent",
                "SitePowerBudgetEvent",
                "TariffBookEvent",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Given_an_event_with_a_driver_s_e_mail_address_then_the_check_finds_it()
    {
        IsEvent(typeof(DriverNotified)).ShouldBeTrue();

        PersonalIn(typeof(DriverNotified), nameof(DriverNotified), [])
            .ShouldBe(["DriverNotified.Contact.Email", "DriverNotified.DriverName"]);
    }

    private static bool IsEvent(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && Ancestry(type).Any(IsEventBase)
        && Ancestry(type).Any(ancestor => ancestor.CustomAttributes.Any(attribute =>
            attribute.AttributeType.Name == "IsClosedTypeAttribute"));

    private static bool IsEventBase(Type type) =>
        type.Name.EndsWith("Event", StringComparison.Ordinal);

    private static IEnumerable<Type> Ancestry(Type type)
    {
        for (Type? current = type; current is not null && current != typeof(object);
             current = current.BaseType)
        {
            yield return current;
        }
    }

    /// <summary>Each property that looks personal, as a path through the event's values.</summary>
    private static IEnumerable<string> PersonalIn(Type type, string path, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var propertyPath = $"{path}.{property.Name}";
            if (LooksPersonal().IsMatch(property.Name)
                && !NotPersonal.Contains((property.DeclaringType!, property.Name)))
            {
                yield return propertyPath;
            }

            var held = HeldTypes(property.PropertyType).Where(IsOhmline).SelectMany(Cases);
            foreach (var value in held)
            {
                foreach (var nested in PersonalIn(value, propertyPath, seen))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>An abstract type, such as a closed record, with every case it may hold.</summary>
    private static IEnumerable<Type> Cases(Type type) =>
        type.IsAbstract
            ? type.Assembly.GetTypes()
                .Where(candidate => !candidate.IsAbstract && type.IsAssignableFrom(candidate))
                .Prepend(type)
            : [type];

    private static bool IsOhmline(Type type) =>
        type.Assembly.GetName().Name!.StartsWith("Ohmline.", StringComparison.Ordinal);

    private static IEnumerable<Type> HeldTypes(Type type) =>
        type switch
        {
            { IsArray: true } => [type.GetElementType()!],
            { IsGenericType: true } => type.GetGenericArguments(),
            _ => [type],
        };

    [GeneratedRegex(
        """
        ^( \w*(Email|EMail|Phone|Mobile|Address|Birth\w*)
         | (First|Last|Full|Given|Family|Middle|Driver|Holder|Customer|Person)?Name )$
        """,
        RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex LooksPersonal();

    private closed record NotificationEvent;

    private sealed record Contact(string Email);

    private sealed record DriverNotified(string DriverName, Contact Contact) : NotificationEvent;
}
