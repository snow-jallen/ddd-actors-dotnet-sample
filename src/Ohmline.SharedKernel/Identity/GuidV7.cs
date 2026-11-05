using System.Security.Cryptography;
using System.Text;

namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// Time-ordered identifiers (RFC 9562 version 7). The time comes from the injected clock, never
/// from DateTime.UtcNow, so tests decide what it is.
/// </summary>
public static class GuidV7
{
    public static Guid New(TimeProvider clock) => Guid.CreateVersion7(clock.GetUtcNow());

    /// <summary>
    /// The id of something that follows from <paramref name="origin"/> for one
    /// <paramref name="purpose"/>: the same pair gives the same id every time, and different
    /// pairs give different ids. It keeps the origin's time, so it still sorts where the origin
    /// does, and takes its other bits from a SHA-256 hash of the pair.
    /// </summary>
    public static Guid Derive(Guid origin, string purpose)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{origin:D}/{purpose}"), hash);

        Span<byte> bytes = stackalloc byte[16];
        origin.TryWriteBytes(bytes, bigEndian: true, out _);
        hash[..10].CopyTo(bytes[6..]);
        bytes[6] = (byte)(0x70 | (bytes[6] & 0x0F)); // Version 7.
        bytes[8] = (byte)(0x80 | (bytes[8] & 0x3F)); // The RFC 9562 variant.
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>The check behind the TryCreate of each Guid id; the name says which id.</summary>
    public static Result<Guid, ValidationError> Check(string name, Guid value) => value switch
    {
        { Version: 7 } => Result.Ok(value),
        _ when value == Guid.Empty => Result.Fail<ValidationError>(new Missing(name)),
        _ => Result.Fail<ValidationError>(new Malformed(name, $"{value}", "a version 7 Guid")),
    };
}
