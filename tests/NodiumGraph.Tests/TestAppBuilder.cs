using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(NodiumGraph.Tests.TestAppBuilder))]

namespace NodiumGraph.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
