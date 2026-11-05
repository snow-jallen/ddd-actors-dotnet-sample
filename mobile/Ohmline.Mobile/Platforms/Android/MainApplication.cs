using Android.App;
using Android.Runtime;

namespace Ohmline.Mobile;

#if DEBUG
// A Debug build talks plain HTTP to the node on the development machine, which the emulator
// reaches as 10.0.2.2, and to nothing else (Resources/xml/network_security_config.xml).
[Application(NetworkSecurityConfig = "@xml/network_security_config")]
#else
[Application]
#endif
public sealed class MainApplication(IntPtr handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
