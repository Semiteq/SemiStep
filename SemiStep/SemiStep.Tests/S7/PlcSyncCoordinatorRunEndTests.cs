using System.Collections.Concurrent;

using AwesomeAssertions;

using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

/// <summary>A recipe parked by RecipeActiveError waits for the run end; nothing else does.</summary>
[Trait("Component", "S7")]
[Trait("Area", "SyncCoordinator")]
[Trait("Category", "Unit")]
public sealed class PlcSyncCoordinatorRunEndTests : IDisposable
{
	private const int PrecheckRead = 1;
	private const int LastReadOfTransaction = 2;

	private static readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(20);
	private static readonly TimeSpan _quietPeriod = TimeSpan.FromMilliseconds(1500);

	private readonly ConcurrentQueue<IError> _faults = new();
	private readonly PlcSyncCoordinator _coordinator;
	private readonly FakeS7Transport _transport;
	private readonly StubS7ServiceForSync _connection;

	public PlcSyncCoordinatorRunEndTests()
	{
		(_coordinator, _transport, _connection) = PlcSyncCoordinatorTests.Build(connected: true);
		_coordinator.Faults.Subscribe(_faults.Enqueue);
	}

	private static CancellationToken TestToken => TestContext.Current.CancellationToken;

	public void Dispose()
	{
		_coordinator.Dispose();
	}

	[Fact]
	public async Task RunEndDuringHeldPrecheck_WritesTheRecipe()
	{
		var hold = HoldExecutionRead(PrecheckRead);
		ReportRunOnFirstPrecheckOnly();
		PushRecipeActive(true);

		_coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		PushRecipeActive(false);
		hold.Release();

		await TestHelpers.WaitUntilAsync(
			() => _coordinator.Status == PlcSyncStatus.Synced,
			_waitTimeout,
			_pollInterval,
			TestToken);
		_transport.WriteLog.Should().NotBeEmpty("a run end seen while the pre-check ran must not be lost");
	}

	[Fact]
	public async Task RunEndAfterFailedAttemptWithQueuedRecipe_StartsNoWrite()
	{
		var stringDbNumber = PlcSyncCoordinatorTests.BuildTestConfiguration().Layout.StringDb.DbNumber;
		var hold = new TransportHold(
			stringDbNumber,
			LastReadOfTransaction,
			TaskCreationOptions.RunContinuationsAsynchronously);
		_transport.BeforeRead = hold.OnCall;
		PushRecipeActive(true);

		_coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		var transactionWrites = _transport.WriteLog.Count;
		_coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		hold.Fail(new InvalidOperationException("verify read failed"));
		await TestHelpers.WaitUntilAsync(
			() => _coordinator.Status == PlcSyncStatus.Failed,
			_waitTimeout,
			_pollInterval,
			TestToken);

		PushRecipeActive(false);
		await Task.Delay(_quietPeriod, TestToken);

		_transport.WriteLog.Should().HaveCount(transactionWrites,
			"only a recipe parked by an active run waits for the run end");
	}

	[Fact]
	public async Task InvalidEditDuringHeldPrecheck_RunEnd_StartsNoWrite()
	{
		var hold = HoldExecutionRead(PrecheckRead);
		ReportRunOnFirstPrecheckOnly();
		PushRecipeActive(true);

		_coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		_coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);
		hold.Release();
		await WaitForRecipeActiveFaultAsync();

		PushRecipeActive(false);
		await Task.Delay(_quietPeriod, TestToken);

		_transport.WriteLog.Should().BeEmpty("an invalid session recipe never reaches the PLC at the run end");
	}

	[Fact]
	public async Task InvalidEditWhileParked_RunEnd_StartsNoWrite()
	{
		await ParkAsync();
		_coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		PushRecipeActive(false);
		await Task.Delay(_quietPeriod, TestToken);

		_transport.WriteLog.Should().BeEmpty("an invalid session recipe never reaches the PLC at the run end");
	}

	[Fact]
	public async Task SyncDisabledWhileParked_RunEnd_StartsNoWrite()
	{
		await ParkAsync();
		_coordinator.SetSyncEnabled(false);
		_coordinator.ResetForDisable();
		_coordinator.SetSyncEnabled(true);

		PushRecipeActive(false);
		await Task.Delay(_quietPeriod, TestToken);

		_transport.WriteLog.Should().BeEmpty("disabling sync clears the park");
	}

	private async Task ParkAsync()
	{
		ReportRunOnFirstPrecheckOnly();
		PushRecipeActive(true);
		_coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await WaitForRecipeActiveFaultAsync();
	}

	private Task WaitForRecipeActiveFaultAsync()
	{
		return TestHelpers.WaitUntilAsync(
			() => _faults.OfType<RecipeActiveError>().Any(),
			_waitTimeout,
			_pollInterval,
			TestToken);
	}

	private TransportHold HoldExecutionRead(int heldCall)
	{
		var executionDbNumber = PlcSyncCoordinatorTests.BuildTestConfiguration().Layout.ExecutionDb.DbNumber;
		var hold = new TransportHold(executionDbNumber, heldCall, TaskCreationOptions.RunContinuationsAsynchronously);
		_transport.BeforeRead = hold.OnCall;

		return hold;
	}

	/// <summary>The first pre-check reads an active run; every later read sees it ended.</summary>
	private void ReportRunOnFirstPrecheckOnly()
	{
		var executionDb = PlcSyncCoordinatorTests.BuildTestConfiguration().Layout.ExecutionDb;
		var executionReads = 0;
		_transport.SetReadResponseForDb(executionDb.DbNumber, (_, count) =>
		{
			var bytes = new byte[count];
			if (Interlocked.Increment(ref executionReads) == 1)
			{
				bytes[executionDb.RecipeActiveOffset] = 1;
			}

			return bytes;
		});
	}

	private void PushRecipeActive(bool isActive)
	{
		_connection.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = isActive });
	}
}
