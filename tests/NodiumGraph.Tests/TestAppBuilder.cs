using Avalonia;
using Avalonia.Headless;

// PerAssembly isolation keeps a single dispatcher thread across all tests in
// the assembly. Required because library statics (e.g. the Default*Brush singletons
// on NodiumGraphCanvas) acquire thread affinity on first access — per-test isolation
// would rotate the dispatcher and trip VerifyAccess on any reuse through the
// real Skia drawing backend.
[assembly: AvaloniaTestApplication(typeof(NodiumGraph.Tests.TestAppBuilder))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace NodiumGraph.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            // UseSkia + UseHeadlessDrawing=false swaps the fake headless drawing
            // backend for the real Skia renderer. This is required for geometry
            // hit-testing (Geometry.StrokeContains / FillContains / GetWidenedGeometry)
            // to return real results in tests — the fake headless backend returns
            // empty bounds for stroke widening and false for stroke-contains.
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
