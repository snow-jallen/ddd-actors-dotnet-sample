namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// The connection actor's way to its charger: a frame out, or the end of the connection. Never
/// blocks: the socket is written by its own loop, outside the actor.
/// </summary>
public interface IChargerLink
{
    void Send(string frame);

    void Close();
}
