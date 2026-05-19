using FluentAssertions;

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
	private static PlcConfiguration BuildConfiguration(
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
			PlcConfiguration: PlcConfiguration.Default);

		return new RecipeMetadataRegistry(config);
	}

	private static (S7Service Service, FakeS7Driver Driver) BuildService(PlcConfiguration configuration)
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
			driver, monitor, executor, configuration, NullLogger<S7Service>.Instance);

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
		service.StateChanged += state => emittedStates.Add(state);

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
		service.StateChanged += state => emittedStates.Add(state);

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
		service.StateChanged += state => statesAfterDisconnect.Add(state);

		// Configure the transport to throw so that any lingering keep-alive would
		// trigger OnConnectionLost if it were still running.
		driver.SetReadExceptionForDb(
			configuration.Layout.ManagingDb.DbNumber,
			new IOException("post-disconnect read should not happen"));

		// (c) Defensive settle: negative assertion — must allow time for any lingering keep-alive
		// tick (50ms interval) to have fired had it not been stopped. No observable predicate exists
		// for "no event was raised", so a bounded wait is required.
		await Task.Delay(200, TestContext.Current.CancellationToken);

		statesAfterDisconnect.Should().NotContain(PlcConnectionState.Disconnected,
			"the keep-alive loop must be fully stopped before DisconnectAsync returns");

		await service.DisposeAsync();
	}
}
