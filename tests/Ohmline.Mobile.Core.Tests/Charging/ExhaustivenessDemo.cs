#if DEMO_COMPILE_ERRORS
using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Core.Tests.Charging;

/// <summary>
/// Build with <c>-p:DefineConstants=DEMO_COMPILE_ERRORS</c> to see a screen that forgot a
/// state: the switch leaves out <see cref="Offline"/>, and the build fails with CS8509, an
/// error because warnings are errors in the app too (section 19.5).
/// </summary>
internal static class ExhaustivenessDemo
{
    public static string Render(ScreenState screen) => screen switch
    {
        Loading => "Sending...",
        Ready => "",
        Failed failed => failed.Message,
        null => "",
    };
}
#endif
