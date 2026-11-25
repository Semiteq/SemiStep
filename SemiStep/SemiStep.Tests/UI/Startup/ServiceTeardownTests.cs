using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.UI;
using SemiStep.UI.Coordinator;
using SemiStep.UI.MainWindow;

using Serilog.Events;

using Xunit;

using static SemiStep.Tests.Helpers.EmulatorTestSupport;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.Startup;

[Trait("Component", "UI")]
[Trait("Area", "Shutdown")]
public sealed class ServiceTeardownTests
{
	private const string DisconnectedTemplate = "Disconnected from PLC";

	private static readonly TimeSpan _shortBound = TimeSpan.FromMilliseconds(200);
	private static readonly TimeSpan _realBound = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan _connectLimit = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan _boundedReturnLimit = TimeSpan.FromSeconds(2);

	[AvaloniaFact]
	[Trait("Category", "Integration")]
	public async Task RealContainer_AfterTheWindowCloses_TearsDownOnceWithoutErrors()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		var options = StartupOptions.Parse(
			[
				"--config-dir", ShippedConfigLocator.GetConfigDirectory("RIE"),
				"--plc-endpoint", $"127.0.0.1:{plc.Port}",
			]);
		var loaded = await Program.LoadConfigurationAsync(options);
		var sink = new CapturingSink();

		using (StaticLoggerSwap.To(sink))
		{
			var provider = Program.BuildServices(options, loaded.Value);
			ResolveAsTheAppDoes(provider);
			await provider.GetRequiredService<IS7Connection>()
				.ConnectAsync(provider.GetRequiredService<PlcConfiguration>().Connection)
				.WaitAsync(_connectLimit, TestContext.Current.CancellationToken);
			var window = new MainWindowView { DataContext = provider.GetRequiredService<MainWindowViewModel>() };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			window.Close();
			Dispatcher.UIThread.RunJobs();

			Program.DisposeServices(provider, _realBound);
		}

		sink.Events.Should().NotContain(e => e.Level >= LogEventLevel.Warning);
		sink.Events.Count(e => e.MessageTemplate.Text == DisconnectedTemplate).Should().Be(1);
	}

	[AvaloniaFact]
	[Trait("Category", "Integration")]
	public async Task RealContainer_NeverConnected_TearsDownWithoutWarnings()
	{
		var options = StartupOptions.Parse(["--config-dir", ShippedConfigLocator.GetConfigDirectory("RIE")]);
		var loaded = await Program.LoadConfigurationAsync(options);
		var sink = new CapturingSink();

		using (StaticLoggerSwap.To(sink))
		{
			var provider = Program.BuildServices(options, loaded.Value);
			ResolveAsTheAppDoes(provider);

			Program.DisposeServices(provider, _realBound);
		}

		sink.Events.Should().NotContain(e => e.Level >= LogEventLevel.Warning);
	}

	[Fact]
	[Trait("Category", "Unit")]
	public async Task TeardownThatNeverEnds_ReturnsAfterTheBoundWithOneWarning()
	{
		var services = new ServiceCollection();
		services.AddSingleton<NeverEndingDisposable>();
		var provider = services.BuildServiceProvider();
		provider.GetRequiredService<NeverEndingDisposable>();
		var sink = new CapturingSink();

		using (StaticLoggerSwap.To(sink))
		{
			var cancellationToken = TestContext.Current.CancellationToken;
			await Task.Run(() => Program.DisposeServices(provider, _shortBound), cancellationToken)
				.WaitAsync(_boundedReturnLimit, cancellationToken);
		}

		sink.Events.Should().ContainSingle().Which.Level.Should().Be(LogEventLevel.Warning);
	}

	[Fact]
	[Trait("Category", "Unit")]
	public void TeardownThatThrows_ReturnsNormallyWithOneError()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ThrowingDisposable>();
		var provider = services.BuildServiceProvider();
		provider.GetRequiredService<ThrowingDisposable>();
		var sink = new CapturingSink();

		using (StaticLoggerSwap.To(sink))
		{
			Program.DisposeServices(provider, _realBound);
		}

		sink.Events.Should().ContainSingle().Which.Level.Should().Be(LogEventLevel.Error);
	}

	private static void ResolveAsTheAppDoes(ServiceProvider provider)
	{
		provider.GetRequiredService<RecipeSession>().Reset();
		provider.GetRequiredService<RecipeCoordinator>().Initialize();
		provider.GetRequiredService<GridStyleOptions>();
		provider.GetRequiredService<MainWindowViewModel>().Initialize();
		provider.GetRequiredService<PlcLifecycleManager>();
	}

	private sealed class NeverEndingDisposable : IAsyncDisposable
	{
		public ValueTask DisposeAsync()
		{
			return new ValueTask(new TaskCompletionSource().Task);
		}
	}

	private sealed class ThrowingDisposable : IAsyncDisposable
	{
		public ValueTask DisposeAsync()
		{
			throw new InvalidOperationException("teardown failure");
		}
	}
}
