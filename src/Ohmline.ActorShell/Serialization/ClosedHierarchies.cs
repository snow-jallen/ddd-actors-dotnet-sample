using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ohmline.ActorShell.Serialization;

/// <summary>
/// A closed record hierarchy inside a message, such as a tariff's components, needs to say which
/// case it is: without that, System.Text.Json writes only the base's members and the case's
/// fields are lost (section 3.9). Each case gets a permanent kind, written as <c>"$kind"</c>, and
/// chosen like a manifest: by hand, never from the type's name.
/// </summary>
public static class ClosedHierarchies
{
    public const string KindProperty = "$kind";

    extension(JsonSerializerOptions options)
    {
        /// <summary>
        /// Writes every <typeparamref name="TBase"/> with its case's kind and reads it back by
        /// that kind. Every concrete case must be listed, or this throws.
        /// </summary>
        public void AddClosedHierarchy<TBase>(params IEnumerable<(Type Case, string Kind)> cases)
        {
            var listed = cases.ToList();
            var concrete = typeof(TBase).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(TBase).IsAssignableFrom(type));
            if (concrete.Except(listed.Select(entry => entry.Case)).FirstOrDefault() is { } missing)
            {
                throw new ArgumentException(
                    $"{missing} is a {typeof(TBase).Name} without a kind.", nameof(cases));
            }

            var resolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
            options.TypeInfoResolver = resolver.WithAddedModifier(info =>
                {
                    if (info.Type != typeof(TBase))
                    {
                        return;
                    }

                    info.PolymorphismOptions = new JsonPolymorphismOptions
                    {
                        TypeDiscriminatorPropertyName = KindProperty,
                        UnknownDerivedTypeHandling =
                            JsonUnknownDerivedTypeHandling.FailSerialization,
                    };
                    foreach (var (type, kind) in listed)
                    {
                        info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, kind));
                    }
                });
        }
    }
}
