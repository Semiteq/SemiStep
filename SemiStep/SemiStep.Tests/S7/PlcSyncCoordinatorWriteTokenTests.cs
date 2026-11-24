using AwesomeAssertions;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "SyncCoordinator")]
[Trait("Category", "Unit")]
public sealed class PlcSyncCoordinatorWriteTokenTests
{
	private const int FirstArrayWrite = 1;

	private static readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan _disposeBoundWait = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(20);
	private static readonly TimeSpan _disposeBlockProbe = TimeSpan.FromMilliseconds(300);

	private static CancellationToken TestToken => TestContext.Current.CancellationToken;

	private static (
		PlcSyncCoordinator Coordinator,
		FakeS7Transport Transport,
		TransportHold Hold,
		RecordingLoggerFactory Logs) BuildHeldArrayWrite()
	{
		var logs = new RecordingLoggerFactory();
		var (coordinator, transport, _) = PlcSyncCoordinatorTests.Build(connected: true, logs);
		var intDbNumber = PlcSyncCoordinatorTests.BuildTestConfiguration().Layout.IntDb.DbNumber;
		var hold = new TransportHold(intDbNumber, FirstArrayWrite, TaskCreationOptions.RunContinuationsAsynchronously);
		transport.BeforeWrite = hold.OnCall;

		return (coordinator, transport, hold, logs);
	}

	private static int CoordinatorWarnings(RecordingLoggerFactory logs)
	{
		return logs.Snapshot<PlcSyncCoordinator>().Count(entry => entry.Level == LogLevel.Warning);
	}

	private static void ShouldCommitAfterLastArrayWrite(FakeS7Transport transport)
	{
		var layout = PlcSyncCoordinatorTests.BuildTestConfiguration().Layout;
		int[] arrayDbNumbers = [layout.IntDb.DbNumber, layout.FloatDb.DbNumber, layout.StringDb.DbNumber];
		var writes = transport.WriteLog.ToList();
		var lastArrayWrite = writes.FindLastIndex(write => arrayDbNumbers.Contains(write.DbNumber));

		lastArrayWrite.Should().BeGreaterThanOrEqualTo(0, "the transaction writes the recipe arrays");
		writes.Skip(lastArrayWrite + 1).Should().Contain(
			write => write.DbNumber == layout.ManagingDb.DbNumber
				&& write.StartByte == layout.ManagingDb.CommittedOffset
				&& write.Data[0] == 1,
			"a started write must end with the committed byte set after the last array write");
	}

	[Fact]
	public async Task ResetForDisableDuringHeldArrayWrite_CompletesTheTransaction()
	{
		var (coordinator, transport, hold, logs) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		coordinator.ResetForDisable();

		hold.HeldToken.IsCancellationRequested.Should().BeFalse("a disable must not cancel a started write");
		hold.Release();
		await coordinator.WaitForPendingSyncAsync(TestToken);

		ShouldCommitAfterLastArrayWrite(transport);
		coordinator.Status.Should().Be(PlcSyncStatus.Idle, "the outcome of the write is stale after the reset");
		CoordinatorWarnings(logs).Should().Be(0, "a stale write that succeeded left the PLC committed");
	}

	[Fact]
	public async Task DisposeDuringHeldArrayWrite_WaitsAndCompletesTheTransaction()
	{
		var (coordinator, transport, hold, _) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		var disposeTask = Task.Run(coordinator.Dispose, TestToken);
		await Task.Delay(_disposeBlockProbe, TestToken);

		disposeTask.IsCompleted.Should().BeFalse("Dispose must wait for the write in flight");
		hold.HeldToken.IsCancellationRequested.Should().BeFalse("Dispose must not cancel a write inside its bound");
		hold.Release();
		await disposeTask.WaitAsync(_waitTimeout, TestToken);

		ShouldCommitAfterLastArrayWrite(transport);
	}

	[Fact]
	public async Task ConnectionLostDuringHeldArrayWrite_CancelsTheWriteAndLogsOneWarning()
	{
		var (coordinator, _, hold, logs) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		coordinator.HandleConnectionLost();

		hold.HeldToken.IsCancellationRequested.Should().BeTrue("a lost connection cancels the write");
		hold.Release();
		await coordinator.WaitForPendingSyncAsync(TestToken);

		CoordinatorWarnings(logs).Should().Be(1, "a cancelled write is logged once");
	}

	[Fact]
	public async Task DisposeWithArrayWriteHeldPastTheBound_CancelsTheWriteAndLogsBothWarnings()
	{
		var (coordinator, _, hold, logs) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		await Task.Run(coordinator.Dispose, TestToken).WaitAsync(_disposeBoundWait, TestToken);

		hold.HeldToken.IsCancellationRequested.Should().BeTrue("Dispose cancels the write once its bound expires");
		hold.Release();
		await TestHelpers.WaitUntilAsync(() => CoordinatorWarnings(logs) >= 2, _waitTimeout, _pollInterval, TestToken);

		CoordinatorWarnings(logs).Should().Be(2, "one Warning for the timeout and one for the aborted write");
	}

	[Fact]
	public async Task HeldArrayWriteFailsNotConnected_LogsOneWarning()
	{
		var (coordinator, _, hold, logs) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		hold.Fail(new DriverNotConnectedException());
		await coordinator.WaitForPendingSyncAsync(TestToken);

		coordinator.Status.Should().Be(PlcSyncStatus.Failed);
		CoordinatorWarnings(logs).Should().Be(1,
			"a NotConnectedError write failure is logged nowhere else above Debug");
	}

	[Fact]
	public async Task HeldArrayWriteFailsAfterResetForDisable_LogsOneStaleWarning()
	{
		var (coordinator, _, hold, logs) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		coordinator.ResetForDisable();
		hold.Fail(new InvalidOperationException("array write failed"));
		await coordinator.WaitForPendingSyncAsync(TestToken);

		coordinator.Status.Should().Be(PlcSyncStatus.Idle, "the outcome of the write is stale after the reset");
		CoordinatorWarnings(logs).Should().Be(1, "a failed write whose outcome is dropped is logged once");
	}

	[Fact]
	public async Task WaitForPendingSyncDuringHeldArrayWrite_ThrowsWhenItsTokenCancels()
	{
		var (coordinator, _, hold, _) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		using var waitCts = new CancellationTokenSource();
		var waitTask = coordinator.WaitForPendingSyncAsync(waitCts.Token);
		waitCts.Cancel();

		var wait = () => waitTask.WaitAsync(_waitTimeout, TestToken);

		await wait.Should().ThrowAsync<OperationCanceledException>("the wait observes its token while the write runs");

		hold.Release();
		await coordinator.WaitForPendingSyncAsync(TestToken);
	}

	[Fact]
	public async Task HeldArrayWriteFailsWithCommandError_LogsNoCoordinatorWarning()
	{
		var (coordinator, _, hold, logs) = BuildHeldArrayWrite();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		hold.Fail(new InvalidOperationException("array write failed"));
		await coordinator.WaitForPendingSyncAsync(TestToken);

		coordinator.Status.Should().Be(PlcSyncStatus.Failed);
		CoordinatorWarnings(logs).Should().Be(0, "the transaction executor already logs this failure as an Error");
	}
}
