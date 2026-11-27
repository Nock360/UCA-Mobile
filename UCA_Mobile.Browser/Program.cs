using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using UCA_Mobile;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out", new Avalonia.Browser.BrowserPlatformOptions { RenderingMode = new[] { Avalonia.Browser.BrowserRenderingMode.Software2D } });

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
