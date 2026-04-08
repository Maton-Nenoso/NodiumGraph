using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(NodiumGraph.Avalonia.Tests.TestAppBuilder))]

namespace NodiumGraph.Avalonia.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
