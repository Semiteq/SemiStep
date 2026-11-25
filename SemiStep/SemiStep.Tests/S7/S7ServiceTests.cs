using System.Collections.Concurrent;

using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "KeepAlive")]
[Trait("Category", "Unit")]
public sealed class S7ServiceTests
{
	internal static PlcConfiguration BuildConfiguration(
		int keepAliveIntervalMs = 50,
		int pollingIntervalMs = 100000)
	{
		var protocolSettings = PlcProtocolSettings.Default with
		{
			KeepAliveIntervalMs = keepAliveIntervalMs,
			PollingIntervalMs = pollingIntervalMs,
		};

		return new PlcConfiguration(
			PlcConnectionSettings.Default,
			protocolSettings,
			PlcProtocolLayout.Default);
	}

	private static RecipeMetadataRegistry BuildMinimalRecipeMetadataRegistry()
	{
		var config = new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: new AppUiOptions("ru"));

		return new RecipeMetadataRegistry(config);
	}

	internal static (S7Service Service, FakeS7Driver Driver) BuildService(
		PlcConfiguration configuration,
		ILogger<S7Service>? logger = null)
	{
		var driver = new FakeS7Driver();
		var converter = new RecipeConverter(BuildMinimalRecipeMetadataRegistry());
		var arrayCodec = TestArrayCodecFactory.Create(configuration);
		var executor = new PlcTransactionExecutor(
			driver, converter, arrayCodec, configuration, NullLogger<PlcTransactionExecutor>.Instance);

		S7Service? service = null;
		var monitor = new PlcExecutionMonitor(
			executor,
			configuration.ProtocolSettings,
			onConnectionLost: () => service!.OnConnectionLost(),
			NullLogger<PlcExecutionMonitor>.Instance);

		service = new S7Service(
			driver, monitor, executor, configuration, logger ?? NullLogger<S7Service>.Instance);

		return (service, driver);
	}

	[Fact]
	public async Task KeepAlive_WhenTransportFails_EmitsDisconnected()
	{
		var configuration = BuildConfiguration(keepAliveIntervalMs: 50, pollingIntervalMs: 100000);
		var (service, driver) = BuildService(configuration);

		var managingDbNumber = configuration.Layout.ManagingDb.DbNumber;
		driver.SetReadExceptionForDb(managingDbNumber, new IOException("simulated connection loss"));

		var emittedStates = new List<PlcConnectionState>();
		service.StateChanged += emittedStates.Add;

		await service.ConnectAsync(PlcConnectionSettings.Default);

		// (a) Observable state: wait until the keep-alive probe detects the simulated failure and emits Disconnected.
		await TestHelpers.WaitUntilAsync(
			() => emittedStates.Contains(PlcConnectionState.Disconnected),
			cancellationToken: TestContext.Current.CancellationToken);

		emittedStates.Should().Contain(PlcConnectionState.Disconnected,
			"the keep-alive probe should detect the transport failure and emit Disconnected");

		await service.DisposeAsync();
	}

	[Fact]
	public async Task ExecutionMonitorCallback_WhenPlcNotConnected_EmitsDisconnected()
	{
		var configuration = BuildConfiguration(keepAliveIntervalMs: 100000, pollingIntervalMs: 50);
		var (service, driver) = BuildService(configuration);

		var emittedStates = new List<PlcConnectionState>();
		service.StateChanged += emittedStates.Add;

		await service.ConnectAsync(PlcConnectionSettings.Default);

		// Transport now reports IsConnected = false, causing NotConnectedError on the next poll.
		driver.SetConnected(false);

		// (a) Observable state: wait until the execution-monitor callback emits Disconnected.
		await TestHelpers.WaitUntilAsync(
			() => emittedStates.Contains(PlcConnectionState.Disconnected),
			cancellationToken: TestContext.Current.CancellationToken);

		emittedStates.Should().Contain(PlcConnectionState.Disconnected,
			"the execution monitor callback should detect the connection loss and emit Disconnected");

		await service.DisposeAsync();
	}

	[Fact]
	public async Task DisconnectAsync_StopsKeepAlive_NoStateChangeAfterDisconnect()
	{
		var configuration = BuildConfiguration(keepAliveIntervalMs: 50, pollingIntervalMs: 100000);
		var (service, driver) = BuildService(configuration);

		await service.ConnectAsync(PlcConnectionSettings.Default);
		await service.DisconnectAsync();

		// Record state changes only after DisconnectAsync completes.
		var statesAfterDisconnect = new List<PlcConnectionState>();
		service.StateChanged += statesAfterDisconnect.Add;

		// Configure the transport to throw so that any lingering keep-alive would
		// trigger OnConnectionLost if it were still running.
		driver.SetReadExceptionForDb(
			configuration.Layout.ManagingDb.DbNumber,
			new IOException("post-disconnect read should not happen"));

		// (c) Defensive settle: negative assertion: must allow time for any lingering keep-alive
		// tick (50ms interval) to have fired had it not been stopped. No observable predicate exists
		// for "no event was raised", so a bounded wait is required.
		await Task.Delay(200, TestContext.Current.CancellationToken);

		statesAfterDisconnect.Should().NotContain(PlcConnectionState.Disconnected,
			"the keep-alive loop must be fully stopped before DisconnectAsync returns");

		await service.DisposeAsync();
	}

	[AvaloniaFact]
	public async Task Loops_AfterConnectFromDispatcher_RunWithoutSynchronizationContext()
	{
		var configuration = BuildConfiguration(keepAliveIntervalMs: 50, pollingIntervalMs: 50);
		var (service, driver) = BuildService(configuration);
		var layout = configuration.Layout;

		await service.ConnectAsync(PlcConnectionSettings.Default);
		await TestHelpers.WaitUntilAsync(
			() => driver.Calls.Any(call => call.DbNumber == layout.ManagingDb.DbNumber)
				&& driver.Calls.Any(call => call.DbNumber == layout.ExecutionDb.DbNumber),
			cancellationToken: TestContext.Current.CancellationToken);

		service.OnConnectionLost();
		await TestHelpers.WaitUntilAsync(
			() => driver.ConnectCount >= 2,
			timeout: TimeSpan.FromSeconds(3),
			pollInterval: TestHelpers.DefaultPollInterval,
			cancellationToken: TestContext.Current.CancellationToken);
		await service.DisposeAsync();

		var callsAfterInitialConnect = driver.Calls.Skip(1);
		callsAfterInitialConnect.Should().OnlyContain(call => !call.OnSynchronizationContext,
			"the poll, keep-alive and reconnect loops must not resume on the UI synchronization context");
	}

	[Fact]
	public async Task PollDetectedLoss_ReconnectsWithinThreeSeconds()
	{
		var configuration = BuildConfiguration(keepAliveIntervalMs: 100000, pollingIntervalMs: 50);
		var (service, driver) = BuildService(configuration);
		var emittedStates = new ConcurrentQueue<PlcConnectionState>();

		await service.ConnectAsync(PlcConnectionSettings.Default);
		service.StateChanged += emittedStates.Enqueue;
		driver.SetConnected(false);

		await TestHelpers.WaitUntilAsync(
			() => emittedStates.SkipWhile(state => state != PlcConnectionState.Disconnected)
				.Contains(PlcConnectionState.Connected),
			timeout: TimeSpan.FromSeconds(3),
			pollInterval: TestHelpers.DefaultPollInterval,
			cancellationToken: TestContext.Current.CancellationToken);

		await service.DisposeAsync();
	}

	// Contract pin: passes on master too and guards the disconnect-wins-over-loss contract.
	[Fact]
	public async Task DisconnectDuringConnectionLossHandling_StartsNoReconnect()
	{
		var (service, driver) = BuildService(BuildConfiguration(keepAliveIntervalMs: 100000));
		await service.ConnectAsync(PlcConnectionSettings.Default);
		var disconnect = DisconnectOnFirstLoss(service);
		var statesAfterLoss = new ConcurrentQueue<PlcConnectionState>();
		service.StateChanged += statesAfterLoss.Enqueue;

		service.OnConnectionLost();
		await disconnect.Task.Unwrap();

		// (c) Defensive settle: negative assertion - the reconnect loop waits 1 s before its first attempt,
		// so 1.5 s covers it. No observable predicate exists for "no reconnect started".
		await Task.Delay(1500, TestContext.Current.CancellationToken);

		statesAfterLoss.Should().NotContain(PlcConnectionState.Connecting,
			"an operator disconnect issued while the loss is handled must win over the reconnect");
		driver.ConnectCount.Should().Be(1);

		await service.DisposeAsync();
	}

	// Contract pin: passes on master too and guards reconnect in the session after a raced disconnect.
	[Fact]
	public async Task RacedDisconnect_NextSessionStillReconnects()
	{
		var (service, driver) = BuildService(BuildConfiguration(keepAliveIntervalMs: 100000));
		await service.ConnectAsync(PlcConnectionSettings.Default);
		var disconnect = DisconnectOnFirstLoss(service);
		service.OnConnectionLost();
		await disconnect.Task.Unwrap();

		await service.ConnectAsync(PlcConnectionSettings.Default);
		service.OnConnectionLost();

		await TestHelpers.WaitUntilAsync(
			() => driver.ConnectCount >= 3 && service.IsConnected,
			timeout: TimeSpan.FromSeconds(3),
			pollInterval: TestHelpers.DefaultPollInterval,
			cancellationToken: TestContext.Current.CancellationToken);

		await service.DisposeAsync();
	}

	// Contract pin: passes on master too and guards reconnect after every loss.
	[Fact]
	public async Task RepeatedLoss_ReconnectsEachTime()
	{
		var configuration = BuildConfiguration(keepAliveIntervalMs: 100000, pollingIntervalMs: 50);
		var (service, driver) = BuildService(configuration);
		var executionDbNumber = configuration.Layout.ExecutionDb.DbNumber;
		await service.ConnectAsync(PlcConnectionSettings.Default);

		service.OnConnectionLost();
		await TestHelpers.WaitUntilAsync(
			() => driver.ConnectCount >= 2
				&& service.IsConnected
				&& HasReadAfterConnect(driver, connectNumber: 2, executionDbNumber),
			timeout: TimeSpan.FromSeconds(3),
			pollInterval: TestHelpers.DefaultPollInterval,
			cancellationToken: TestContext.Current.CancellationToken);

		service.OnConnectionLost();
		await TestHelpers.WaitUntilAsync(
			() => driver.ConnectCount >= 3 && service.IsConnected,
			timeout: TimeSpan.FromSeconds(3),
			pollInterval: TestHelpers.DefaultPollInterval,
			cancellationToken: TestContext.Current.CancellationToken);

		await service.DisposeAsync();
	}

	// Contract pin: passes on master too and guards a disconnect during the reconnect delay.
	[Fact]
	public async Task DisconnectDuringReconnectDelay_StartsNoReconnect_NextSessionStillReconnects()
	{
		var (service, driver) = BuildService(BuildConfiguration(keepAliveIntervalMs: 100000));
		await service.ConnectAsync(PlcConnectionSettings.Default);
		service.OnConnectionLost();
		await service.DisconnectAsync();
		var statesAfterDisconnect = new ConcurrentQueue<PlcConnectionState>();
		service.StateChanged += statesAfterDisconnect.Enqueue;

		// (c) Defensive settle: negative assertion - the reconnect loop waits 1 s before its first attempt,
		// so 1.5 s covers it. No observable predicate exists for "no reconnect started".
		await Task.Delay(1500, TestContext.Current.CancellationToken);

		statesAfterDisconnect.Should().NotContain(PlcConnectionState.Connecting,
			"a disconnect during the reconnect delay must stop the reconnect loop");
		driver.ConnectCount.Should().Be(1);

		await service.ConnectAsync(PlcConnectionSettings.Default);
		service.OnConnectionLost();
		await TestHelpers.WaitUntilAsync(
			() => driver.ConnectCount >= 3 && service.IsConnected,
			timeout: TimeSpan.FromSeconds(3),
			pollInterval: TestHelpers.DefaultPollInterval,
			cancellationToken: TestContext.Current.CancellationToken);

		await service.DisposeAsync();
	}

	[Fact]
	public async Task LossBeforeReconnectLoopReleases_ReconnectsAndRestartsKeepAlive()
	{
		var configuration = BuildConfiguration(keepAliveIntervalMs: 50);
		var (service, driver) = BuildService(configuration);
		await service.ConnectAsync(PlcConnectionSettings.Default);
		var lossInjected = 0;
		service.StateChanged += state =>
		{
			// Runs on the reconnect loop after State = Connected and before the loop releases its handles.
			if (state == PlcConnectionState.Connected && Interlocked.Exchange(ref lossInjected, 1) == 0)
			{
				driver.SetReadExceptionForDb(
					configuration.Layout.ManagingDb.DbNumber, new IOException("simulated connection loss"));
				service.OnConnectionLost();
			}
		};

		service.OnConnectionLost();

		// Connect 3 reconnects after the injected loss; connect 4 follows the loss its keep-alive detects.
		await TestHelpers.WaitUntilAsync(
			() => driver.ConnectCount >= 4,
			timeout: TimeSpan.FromSeconds(6),
			pollInterval: TestHelpers.DefaultPollInterval,
			cancellationToken: TestContext.Current.CancellationToken);

		await service.DisposeAsync();
	}

	[Fact]
	public async Task LaggingConnectionLoss_AfterDisconnectAndConnect_StartsNoReconnect()
	{
		var (service, driver) = BuildService(BuildConfiguration(keepAliveIntervalMs: 100000));
		await service.ConnectAsync(PlcConnectionSettings.Default);
		using var releaseLoss = new ManualResetEventSlim();
		var lossBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		service.StateChanged += state =>
		{
			if (state == PlcConnectionState.Disconnected && lossBlocked.TrySetResult())
			{
				releaseLoss.Wait(TimeSpan.FromSeconds(10));
			}
		};

		var laggingLoss = Task.Run(service.OnConnectionLost, TestContext.Current.CancellationToken);
		await lossBlocked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		await service.DisconnectAsync();
		await service.ConnectAsync(PlcConnectionSettings.Default);
		var statesAfterLossResumes = new ConcurrentQueue<PlcConnectionState>();
		service.StateChanged += statesAfterLossResumes.Enqueue;
		releaseLoss.Set();
		await laggingLoss;

		// (c) Defensive settle: negative assertion - the reconnect loop waits 1 s before its first attempt,
		// so 1.5 s covers it. No observable predicate exists for "no reconnect started".
		await Task.Delay(1500, TestContext.Current.CancellationToken);

		statesAfterLossResumes.Should().NotContain(PlcConnectionState.Connecting,
			"a connection loss that resumes after a newer connect must not reconnect that session");
		driver.ConnectCount.Should().Be(2);

		await service.DisposeAsync();
	}

	private static TaskCompletionSource<Task> DisconnectOnFirstLoss(S7Service service)
	{
		var disconnect = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
		service.StateChanged += state =>
		{
			if (state == PlcConnectionState.Disconnected && !disconnect.Task.IsCompleted)
			{
				disconnect.TrySetResult(service.DisconnectAsync());
			}
		};

		return disconnect;
	}

	private static bool HasReadAfterConnect(FakeS7Driver driver, int connectNumber, int dbNumber)
	{
		var connectsSeen = 0;
		foreach (var call in driver.Calls)
		{
			if (call.IsConnect)
			{
				connectsSeen++;
			}
			else if (connectsSeen >= connectNumber && call.DbNumber == dbNumber)
			{
				return true;
			}
		}

		return false;
	}
}
