using Avalonia;
using Avalonia.Wayland;

namespace OsintToolkit.WPF;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect();
        // Prefer XWayland on KDE when it is available; native Wayland remains a
        // fallback for sessions without XWayland and an explicit opt-in option.
        return OperatingSystem.IsLinux()
            && Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null
            && (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                || Environment.GetEnvironmentVariable("OSINT_NATIVE_WAYLAND") == "1")
            ? builder.UseWayland()
            : builder;
    }
}
