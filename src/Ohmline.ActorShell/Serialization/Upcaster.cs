namespace Ohmline.ActorShell.Serialization;

/// <summary>
/// Reads an older manifest version as the type the serializer writes today (section 20.3). The
/// stored JSON is read as the shape it was written in, which only the upcaster still knows, and
/// converted once, in memory: no stored byte is ever rewritten, and no code past the serializer
/// ever sees the old shape.
/// </summary>
public abstract class Upcaster
{
    private protected Upcaster()
    {
    }

    /// <summary>The shape the old manifest's JSON was written in.</summary>
    public abstract Type From { get; }

    /// <summary>The type written today, which the upcaster gives back.</summary>
    public abstract Type To { get; }

    /// <summary>Reads the JSON as <typeparamref name="TOld"/>, then converts it.</summary>
    public static Upcaster Create<TOld, TNew>(Func<TOld, TNew> upcast)
        where TOld : class
        where TNew : class =>
        new Typed<TOld, TNew>(upcast);

    /// <summary>
    /// For a type that was renamed under a new manifest (section 20.7): the same shape, read
    /// under the manifest it was stored with.
    /// </summary>
    public static Upcaster Alias<T>()
        where T : class =>
        new Typed<T, T>(stored => stored);

    /// <summary>Converts what was read from the old JSON into today's type.</summary>
    public abstract object Upcast(object old);

    private sealed class Typed<TOld, TNew>(Func<TOld, TNew> upcast) : Upcaster
        where TOld : class
        where TNew : class
    {
        public override Type From => typeof(TOld);

        public override Type To => typeof(TNew);

        public override object Upcast(object old) => upcast((TOld)old);
    }
}
