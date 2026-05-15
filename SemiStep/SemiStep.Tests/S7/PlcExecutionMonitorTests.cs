using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "ExecutionMonitor")]
[Trait("Category", "Unit")]
public sealed class PlcExecutionMonitorTests
{
	private static PlcConfiguration BuildTestConfiguration()
	{
		var layout = new PlcProtocolLayout(
			ManagingDb: ManagingDbLayout.Default,
			IntDb: new DataDbLayout(DbNumber: 3, CapacityOffset: 0, CurrentSizeOffset: 4, DataStartOffset: 8),
			FloatDb: new DataDbLayout(DbNumber: 4, CapacityOffset: 0, CurrentSizeOffset: 4, DataStartOffset: 8),
			StringDb: new DataDbLayout(DbNumber: 5, CapacityOffset: 0, CurrentSizeOffset: 4, DataStartOffset: 8),
			ExecutionDb: ExecutionDbLayout.Default);

		return new PlcConfiguration(
			PlcConnectionSettings.Default,
			PlcProtocolSettings.Default with { PollingIntervalMs = 50 },
			layout);
	}

	private static RecipeMetadataRegistry BuildEmptyRecipeMetadataRegistry()
	{
		var config = new AppConfiguration(
			Properties: new Dictionary<string, PropertyTypeDefinition>(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

		return new RecipeMetadataRegistry(config);
	}

	/// <summary>
	/// Builds a <see cref="PlcExecutionMonitor"/> backed by a <see cref="FakeExecutionTransport"/>
	/// that returns a controllable execution state on every poll tick.
	/// </summary>
	private static (PlcExecutionMonitor Monitor, FakeExecutionTransport Transport) BuildMonitor(
		PlcExecutionInfo? executionStateToReturn = null)
	{
		var configuration = BuildTestConfiguration();
		var transport = new FakeExecutionTransport(
			configuration.Layout,
			executionStateToReturn ?? new PlcExecutionInfo(
				RecipeActive: false,
				ActualLine: 0,
				StepCurrentTime: 0f,
				ForLoopCount1: 0,
				ForLoopCount2: 0,
				ForLoopCount3: 0));

		var converter = new RecipeConverter(BuildEmptyRecipeMetadataRegistry());
		var executor = new PlcTransactionExecutor(
			transport, converter, configuration, NullLogger<PlcTransactionExecutor>.Instance);
		var monitor = new PlcExecutionMonitor(
			executor,
			configuration.ProtocolSettings,
			onConnectionLost: () => { },
			NullLogger<PlcExecutionMonitor>.Instance);

		return (monitor, transport);
	}

	[Fact]
	public void Stop_PublishesEmptyExecutionInfo()
	{
		var (monitor, _) = BuildMonitor();
		PlcExecutionInfo? received = null;
		monitor.State.Subscribe(info => received = info);

		monitor.Stop();

		received.Should().NotBeNull();
		received!.RecipeActive.Should().BeFalse();
		received.ActualLine.Should().Be(0);
	}

	[Fact]
	public void Stop_WithoutStart_PublishesEmpty()
	{
		var (monitor, _) = BuildMonitor();
		var items = new List<PlcExecutionInfo>();
		monitor.State.Subscribe(info => items.Add(info));

		monitor.Stop();

		items.Should().ContainSingle()
			.Which.Should().Be(PlcExecutionInfo.Empty);
	}

	[Fact]
	public async Task Start_PublishesExecutionStateFromPoll()
	{
		var expectedState = new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 3,
			StepCurrentTime: 1.5f,
			ForLoopCount1: 2,
			ForLoopCount2: 0,
			ForLoopCount3: 0);

		var (monitor, _) = BuildMonitor(expectedState);
		var received = new List<PlcExecutionInfo>();
		monitor.State.Subscribe(info => received.Add(info));

		monitor.Start(TestContext.Current.CancellationToken);

		// (a) Observable state: wait until at least one poll has delivered the expected RecipeActive=true snapshot.
		await TestHelpers.WaitUntilAsync(
			() => received.Any(info => info.RecipeActive),
			cancellationToken: TestContext.Current.CancellationToken);
		monitor.Stop();

		received.Should().Contain(info => info.RecipeActive,
			"at least one poll should have delivered RecipeActive=true");

		var polledInfo = received.First(info => info.RecipeActive);
		polledInfo.ActualLine.Should().Be(3);
		polledInfo.StepCurrentTime.Should().BeApproximately(1.5f, precision: 0.001f);
		polledInfo.ForLoopCount1.Should().Be(2);
	}

	[Fact]
	public async Task Start_ThenStop_LastPublishedValueIsEmpty()
	{
		var (monitor, _) = BuildMonitor(
			new PlcExecutionInfo(
				RecipeActive: true,
				ActualLine: 1,
				StepCurrentTime: 0f,
				ForLoopCount1: 0,
				ForLoopCount2: 0,
				ForLoopCount3: 0));

		PlcExecutionInfo? lastReceived = null;
		monitor.State.Subscribe(info => lastReceived = info);

		monitor.Start(TestContext.Current.CancellationToken);

		// (a) Observable state: wait until at least one poll has delivered RecipeActive=true,
		// so that the subsequent Stop() reliably overwrites it with Empty (RecipeActive=false).
		await TestHelpers.WaitUntilAsync(
			() => lastReceived is { RecipeActive: true },
			cancellationToken: TestContext.Current.CancellationToken);
		monitor.Stop();

		lastReceived.Should().NotBeNull();
		lastReceived!.RecipeActive.Should().BeFalse(
			"Stop() must publish PlcExecutionInfo.Empty as the final value");
	}

	[Fact]
	public async Task Start_MultipleTimes_DoesNotAccumulateMultipleLoops()
	{
		var (monitor, transport) = BuildMonitor();
		monitor.State.Subscribe(_ => { });

		monitor.Start(TestContext.Current.CancellationToken);
		monitor.Start(TestContext.Current.CancellationToken);
		// (b) SUT-internal timer: this test measures the poll rate over a fixed wall-clock window.
		// Replacing with a predicate-based wait would defeat the purpose of bounding poll count.
		// Configured polling interval is 50 ms; 200 ms window allows ~4 expected polls.
		await Task.Delay(200, TestContext.Current.CancellationToken);
		monitor.Stop();

		var readCount = transport.ExecutionReadCount;
		readCount.Should().BeGreaterThan(0, "at least one poll should have occurred");
		readCount.Should().BeLessThan(20,
			"calling Start() twice should not double the poll rate");
	}

	[Fact]
	public void LastKnown_InitiallyEmpty()
	{
		var (monitor, _) = BuildMonitor();

		monitor.LastKnown.Should().Be(PlcExecutionInfo.Empty);
	}

	[Fact]
	public async Task LastKnown_UpdatesAfterPoll()
	{
		var executionState = new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 5,
			StepCurrentTime: 2.0f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0);

		var (monitor, _) = BuildMonitor(executionState);

		monitor.Start(TestContext.Current.CancellationToken);

		// (a) Observable state: poll LastKnown until at least one poll has updated it.
		await TestHelpers.WaitUntilAsync(
			() => monitor.LastKnown.RecipeActive,
			cancellationToken: TestContext.Current.CancellationToken);

		monitor.LastKnown.RecipeActive.Should().BeTrue();
		monitor.LastKnown.ActualLine.Should().Be(5);

		monitor.Stop();
	}

	[Fact]
	public async Task PollLoop_TransientError_ContinuesPolling()
	{
		var configuration = BuildTestConfiguration();
		var successState = new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 7,
			StepCurrentTime: 0f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0);

		var innerTransport = new FakeExecutionTransport(configuration.Layout, successState);
		var transport = new FailFirstExecutionTransport(innerTransport, configuration.Layout.ExecutionDb.DbNumber);
		var converter = new RecipeConverter(BuildEmptyRecipeMetadataRegistry());
		var executor = new PlcTransactionExecutor(
			transport, converter, configuration, NullLogger<PlcTransactionExecutor>.Instance);
		var monitor = new PlcExecutionMonitor(
			executor,
			configuration.ProtocolSettings,
			onConnectionLost: () => { },
			NullLogger<PlcExecutionMonitor>.Instance);

		var received = new List<PlcExecutionInfo>();
		monitor.State.Subscribe(info => received.Add(info));

		monitor.Start(TestContext.Current.CancellationToken);

		// (a) Observable state: wait until the poll loop recovers from the first-failure and delivers a valid result.
		await TestHelpers.WaitUntilAsync(
			() => received.Any(info => info.RecipeActive),
			timeout: TimeSpan.FromMilliseconds(3000),
			pollInterval: TimeSpan.FromMilliseconds(20),
			cancellationToken: TestContext.Current.CancellationToken);
		monitor.Stop();

		received.Should().Contain(info => info.RecipeActive,
			"the poll loop must continue after a transient (non-NotConnectedError) failure and eventually deliver a valid result");
	}

	[Fact]
	public void Dispose_CompletesObservable()
	{
		var (monitor, _) = BuildMonitor();
		var completed = false;
		monitor.State.Subscribe(_ => { }, () => completed = true);

		monitor.Dispose();

		completed.Should().BeTrue("Dispose() must call OnCompleted on the observable subject");
	}
}
