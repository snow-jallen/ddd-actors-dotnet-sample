using System.Buffers.Binary;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DeviceGateway.Ocpp.Translation;

/// <summary>
/// OCPP names a transaction by a number the central system chooses; Ohmline names a session
/// by a Guid. The number is taken from the session id's random bits, so the same session gets
/// the same number every time it is asked for, after a reconnection too.
/// </summary>
public static class TransactionIds
{
    public static int Of(SessionId sessionId)
    {
        Span<byte> bytes = stackalloc byte[16];
        sessionId.Value.TryWriteBytes(bytes, bigEndian: true, out _);
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes[12..]) & 0x7FFF_FFFF) | 1;
    }
}
