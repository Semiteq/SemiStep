using Avalonia;
using Avalonia.Headless;

using ReactiveUI.Avalonia;

using SemiStep.Tests;

using App = SemiStep.UI.App;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace SemiStep.Tests;

public static class TestAppBuilder
{
	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UseHeadless(new AvaloniaHeadlessPlatformOptions())
			.UseReactiveUI(_ => { });
	}
}
