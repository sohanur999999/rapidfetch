using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RapidFetch.App;

/// <summary>
/// Applies the Windows 11 dark material and rounded corners to every app window.
/// The custom glass surfaces are drawn in XAML; these DWM attributes keep the
/// native frame and shadows in step with the dark theme.
/// </summary>
internal static class GlassEffects
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        int dark = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        // Transparent WPF windows draw their own rounded glass card. DWM's
        // acrylic backdrop samples the desktop wallpaper instead of the page
        // behind the popup, which would turn the card into an opaque gray
        // rectangle; disable the backdrop and native corner clipping so the
        // per-pixel transparent card is what reaches the screen.
        if (window.AllowsTransparency)
        {
            int noBackdrop = 1; // DWMSBT_NONE
            _ = DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref noBackdrop, sizeof(int));
            int noRound = 1; // DWMWCP_DONOTROUND
            _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref noRound, sizeof(int));
            return;
        }

        int rounded = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref rounded, sizeof(int));

        int acrylic = 3; // DWMSBT_TRANSIENTWINDOW
        _ = DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref acrylic, sizeof(int));
    }

    public static void ApplyToAllWindows()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Apply((Window)sender)));
    }
}
