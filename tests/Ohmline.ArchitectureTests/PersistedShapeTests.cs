using System.Reflection;
using System.Runtime.CompilerServices;
using Ohmline.SharedKernel;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// ADR 0003 as a test. A closed record hierarchy is what Ohmline tells, persists and puts on the
/// wire: commands, events, replies. A union cannot survive that trip (section 3.9), so no closed
/// hierarchy in a Contracts or Domain assembly may hold a union, however deep in its properties.
/// </summary>
public sealed class PersistedShapeTests
{
    public static TheoryData<string> Projects =>
        new(Repository.ContractsProjects.Concat(Repository.DomainProjects));

    [Theory]
    [MemberData(nameof(Projects))]
    public void A_closed_hierarchy_never_holds_a_union(string project)
    {
        var shapes = Repository.Load(project).GetTypes().Where(IsInClosedHierarchy);

        shapes.SelectMany(shape => UnionsIn(shape, shape.Name, [])).ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_closed_record_that_holds_a_union_then_the_check_finds_it()
    {
        IsInClosedHierarchy(typeof(Smuggler)).ShouldBeTrue();

        UnionsIn(typeof(Smuggler), nameof(Smuggler), []).ShouldBe(["Smuggler.Errors[]"]);
    }

    private static bool IsInClosedHierarchy(Type type) =>
        type.IsClass && Ancestry(type).Any(ancestor =>
            ancestor.CustomAttributes.Any(attribute =>
                attribute.AttributeType.Name == "IsClosedTypeAttribute"));

    private static IEnumerable<Type> Ancestry(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    /// <summary>Each union a type holds, as a path through Ohmline types and collections.</summary>
    private static IEnumerable<string> UnionsIn(Type type, string path, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var (held, suffix) in HeldTypes(property.PropertyType))
            {
                var heldPath = $"{path}.{property.Name}{suffix}";
                if (typeof(IUnion).IsAssignableFrom(held))
                {
                    yield return heldPath;
                }
                else if (IsOhmline(held))
                {
                    foreach (var nested in UnionsIn(held, heldPath, seen))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }

    private static bool IsOhmline(Type type) =>
        type.Assembly.GetName().Name!.StartsWith("Ohmline.", StringComparison.Ordinal);

    /// <summary>A property's type, or the element types of a collection or a nullable.</summary>
    private static IEnumerable<(Type Held, string Suffix)> HeldTypes(Type type) =>
        type switch
        {
            { IsArray: true } => [(type.GetElementType()!, "[]")],
            { IsGenericType: true } => type.GetGenericArguments().Select(held => (held, "[]")),
            _ => [(type, "")],
        };

    private closed record Contraband;

    private sealed record Smuggler(IReadOnlyList<ValidationError> Errors) : Contraband;
}
