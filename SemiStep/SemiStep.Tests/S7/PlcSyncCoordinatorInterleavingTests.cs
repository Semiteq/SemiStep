using System.Reactive.Linq;

using AwesomeAssertions;

using FluentResults;

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
public sealed class PlcSyncCoordinatorInterleavingTests
{
	private const int LastReadOfTransaction = 2;
	private const int PrecheckRead = 1;

	private static readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(20);
	private static readonly TimeSpan _quietPeriod = TimeSpan.FromMilliseconds(1500);
	private static readonly TimeSpan _crossThreadReadTimeout = TimeSpan.FromSeconds(2);
	private static readonly TimeSpan _disposeBlockProbe = TimeSpan.FromMilliseconds(300);

	private static CancellationToken TestToken => TestContext.Current.CancellationToken;

	private static (PlcSyncCoordinator Coordinator, FakeS7Transport Transport, ReadHold Hold) BuildHeld()
	{
		var stringDbNumber = PlcSyncCoordinatorTests.BuildTestConfiguration().Layout.StringDb.DbNumber;

		return BuildHeld(stringDbNumber, LastReadOfTransaction, TaskCreationOptions.RunContinuationsAsynchronously);
	}

	private static (PlcSyncCoordinator Coordinator, FakeS7Transport Transport, ReadHold Hold) BuildHeldPrecheck(
		TaskCreationOptions releaseOptions)
	{
		var executionDbNumber = PlcSyncCoordinatorTests.BuildTestConfiguration().Layout.ExecutionDb.DbNumber;

		return BuildHeld(executionDbNumber, PrecheckRead, releaseOptions);
	}

	private static (PlcSyncCoordinator Coordinator, FakeS7Transport Transport, ReadHold Hold) BuildHeld(
		int dbNumber,
		int heldRead,
		TaskCreationOptions releaseOptions)
	{
		var (coordinator, transport, _) = PlcSyncCoordinatorTests.Build(connected: true);
		var hold = new ReadHold(dbNumber, heldRead, releaseOptions);
		transport.BeforeRead = hold.OnRead;

		return (coordinator, transport, hold);
	}

	[Fact]
	public async Task RecipeQueuedDuringHeldWrite_StartsSecondTransactionAfterRelease()
	{
		var (coordinator, transport, hold) = BuildHeld();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		var transactionWrites = transport.WriteLog.Count;
		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		hold.Release();

		await TestHelpers.WaitUntilAsync(
			() => transport.WriteLog.Count >= 2 * transactionWrites,
			_waitTimeout,
			_pollInterval,
			TestToken);
		await TestHelpers.WaitUntilAsync(
			() => coordinator.Status == PlcSyncStatus.Synced,
			_waitTimeout,
			_pollInterval,
			TestToken);
	}

	[Fact]
	public async Task RecipeQueuedInsideSyncedEmission_StartsSecondTransaction()
	{
		var (coordinator, transport, _) = PlcSyncCoordinatorTests.Build(connected: true);
		var firstTransactionWrites = 0;
		using var subscription = coordinator.PlcState
			.Where(snapshot => snapshot.SyncStatus == PlcSyncStatus.Synced)
			.Take(1)
			.Subscribe(_ =>
			{
				Volatile.Write(ref firstTransactionWrites, transport.WriteLog.Count);
				coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
			});

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		await TestHelpers.WaitUntilAsync(
			() => Volatile.Read(ref firstTransactionWrites) > 0,
			_waitTimeout,
			_pollInterval,
			TestToken);
		await TestHelpers.WaitUntilAsync(
			() => transport.WriteLog.Count >= 2 * Volatile.Read(ref firstTransactionWrites),
			_waitTimeout,
			_pollInterval,
			TestToken);
	}

	[Fact]
	public async Task RecipeQueuedDuringHeldWrite_WriteFails_NoSecondTransaction()
	{
		var (coordinator, transport, hold) = BuildHeld();
		var faults = new List<IError>();
		using var subscription = coordinator.Faults.Subscribe(faults.Add);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		var transactionWrites = transport.WriteLog.Count;
		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		hold.Fail(new InvalidOperationException("verify read failed"));

		await TestHelpers.WaitUntilAsync(() => faults.Count > 0, _waitTimeout, _pollInterval, TestToken);
		await Task.Delay(_quietPeriod, TestToken);

		coordinator.Status.Should().Be(PlcSyncStatus.Failed);
		faults.Should().ContainSingle("a failed write emits exactly one fault");
		transport.WriteLog.Should().HaveCount(transactionWrites,
			"a failed write leaves the queued recipe for the next change and starts no new transaction");
	}

	[Fact]
	public async Task ResetForDisableDuringHeldWrite_DropsTheLateOutcome()
	{
		var (coordinator, _, hold) = BuildHeld();
		var faults = new List<IError>();
		using var subscription = coordinator.Faults.Subscribe(faults.Add);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		coordinator.ResetForDisable();

		hold.Release();
		await coordinator.WaitForPendingSyncAsync(TestToken);

		coordinator.Status.Should().Be(PlcSyncStatus.Idle,
			"an outcome that arrives after a reset must not change the status");
		coordinator.LastSyncTime.Should().BeNull(
			"an outcome that arrives after a reset must not stamp the last sync time");
		faults.Should().BeEmpty();
	}

	[Fact]
	public async Task RecipeQueuedAfterResetDuringHeldWrite_StartsSecondTransactionAfterRelease()
	{
		var (coordinator, transport, hold) = BuildHeld();
		coordinator.SetSyncEnabled(true);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		var transactionWrites = transport.WriteLog.Count;
		coordinator.SetSyncEnabled(false);
		coordinator.ResetForDisable();
		coordinator.SetSyncEnabled(true);
		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		hold.Release();

		await TestHelpers.WaitUntilAsync(
			() => transport.WriteLog.Count >= 2 * transactionWrites,
			_waitTimeout,
			_pollInterval,
			TestToken);
		await TestHelpers.WaitUntilAsync(
			() => coordinator.Status == PlcSyncStatus.Synced,
			_waitTimeout,
			_pollInterval,
			TestToken);
	}

	[Fact]
	public async Task ResetForDisableDuringHeldPrecheck_StartsNoWrite()
	{
		var (coordinator, transport, hold) = BuildHeldPrecheck(TaskCreationOptions.None);
		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		using var subscription = coordinator.PlcState
			.Where(snapshot => snapshot.SyncStatus == PlcSyncStatus.Idle)
			.Subscribe(_ => hold.Release());

		await Task.Run(coordinator.ResetForDisable, TestToken);
		await coordinator.WaitForPendingSyncAsync(TestToken);

		transport.WriteLog.Should().BeEmpty(
			"an attempt whose Syncing outcome was dropped must not start the write on a live token");
	}

	[Fact]
	public async Task ConnectionLostDuringHeldWrite_SetsFailedWithOneConnectionLostFault()
	{
		var (coordinator, _, hold) = BuildHeld();
		var faults = new List<IError>();
		using var subscription = coordinator.Faults.Subscribe(faults.Add);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		coordinator.Status.Should().Be(PlcSyncStatus.Syncing);
		coordinator.HandleConnectionLost();

		hold.Release();
		await coordinator.WaitForPendingSyncAsync(TestToken);

		coordinator.Status.Should().Be(PlcSyncStatus.Failed,
			"a connection lost mid-write must not leave the status at Syncing");
		faults.Should().ContainSingle().Which.Should().BeOfType<ConnectionLostError>();
	}

	[Fact]
	public async Task DisposeDuringHeldWriteWithQueuedRecipe_NoWriteAfterDisposeReturns()
	{
		var (coordinator, transport, hold) = BuildHeld();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitTimeout, TestToken);
		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		var disposeTask = Task.Run(coordinator.Dispose, TestToken);
		await TestHelpers.WaitUntilAsync(
			() => hold.HeldToken.IsCancellationRequested,
			_waitTimeout,
			_pollInterval,
			TestToken);
		var writesAtDispose = transport.WriteLog.Count;

		hold.Release();
		await disposeTask.WaitAsync(_waitTimeout, TestToken);

		coordinator.WaitForPendingSyncAsync(TestToken).IsCompleted.Should().BeTrue(
			"no debounce task may be started after Dispose and outlive it");
		await Task.Delay(_quietPeriod, TestToken);

		transport.WriteLog.Should().HaveCount(writesAtDispose,
			"no debounce may start after Dispose, so no write reaches the PLC after Dispose returns");
	}

	[Fact]
	public void SubscriberReadingStateFromAnotherThreadInsideOnNext_GetsAnswer()
	{
		var (coordinator, _, _) = PlcSyncCoordinatorTests.Build();
		var timeouts = 0;

		void ReadStateFromAnotherThread()
		{
			var read = Task.Run(() => (coordinator.Status, coordinator.IsSyncEnabled));
			if (!read.Wait(_crossThreadReadTimeout))
			{
				Interlocked.Increment(ref timeouts);
			}
		}

		using var stateSubscription = coordinator.PlcState.Skip(1).Subscribe(_ => ReadStateFromAnotherThread());
		using var faultSubscription = coordinator.Faults.Subscribe(_ => ReadStateFromAnotherThread());

		coordinator.SetSyncEnabled(true);
		coordinator.HandleConnectionLost();

		timeouts.Should().Be(0, "no emission may hold the lock that the state getters take");
	}

	[Fact]
	public async Task ConcurrentStateChanges_EmitWithoutOverlapAndEndOnCurrentState()
	{
		var (coordinator, _, _) = PlcSyncCoordinatorTests.Build();
		var insideOnNext = 0;
		var overlaps = 0;
		PlcSessionSnapshot? lastSnapshot = null;
		using var subscription = coordinator.PlcState.Subscribe(snapshot =>
		{
			if (Interlocked.Increment(ref insideOnNext) > 1)
			{
				Interlocked.Increment(ref overlaps);
			}

			Volatile.Write(ref lastSnapshot, snapshot);
			Thread.SpinWait(50);
			Interlocked.Decrement(ref insideOnNext);
		});

		var writers = Enumerable.Range(0, 8).Select(writer => Task.Run(() =>
		{
			for (var change = 0; change < 200; change++)
			{
				coordinator.SetSyncEnabled((writer + change) % 2 == 0);
				coordinator.UpdateConnectionState(
					change % 3 == 0 ? PlcConnectionState.Connected : PlcConnectionState.Disconnected);
			}
		}, TestToken));
		await Task.WhenAll(writers);

		overlaps.Should().Be(0, "emissions must be serialized");
		var last = Volatile.Read(ref lastSnapshot);
		last.Should().NotBeNull();
		last!.IsSyncEnabled.Should().Be(coordinator.IsSyncEnabled, "the last snapshot must equal the current state");
		last.ConnectionState.Should().Be(PlcConnectionState.Disconnected,
			"every writer ends on change 199, which sets Disconnected");
	}

	[Fact]
	public async Task DisposeDuringBlockedEmission_CompletesOnlyAfterTheEmission()
	{
		var (coordinator, _, _) = PlcSyncCoordinatorTests.Build();
		var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var subscription = coordinator.PlcState.Skip(1).Subscribe(_ =>
		{
			arrived.TrySetResult();
			release.Task.Wait(_waitTimeout);
		});

		var emitter = Task.Run(() => coordinator.SetSyncEnabled(true), TestToken);
		Task disposeTask;
		try
		{
			await arrived.Task.WaitAsync(_waitTimeout, TestToken);
			disposeTask = Task.Run(coordinator.Dispose, TestToken);
			await Task.Delay(_disposeBlockProbe, TestToken);

			disposeTask.IsCompleted.Should().BeFalse("Dispose must wait for the emission in progress");
		}
		finally
		{
			release.TrySetResult();
		}

		await emitter.WaitAsync(_waitTimeout, TestToken);
		await disposeTask.WaitAsync(_waitTimeout, TestToken);
	}

	/// <summary>Holds the n-th read of one DB until the test releases it.</summary>
	private sealed class ReadHold(int dbNumber, int heldRead, TaskCreationOptions releaseOptions)
	{
		private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _release = new(releaseOptions);
		private int _reads;

		public Task Reached => _reached.Task;

		public CancellationToken HeldToken { get; private set; }

		/// <summary>The held read ignores its cancellation token, so a released attempt completes as written.</summary>
		public Task OnRead(int readDbNumber, CancellationToken ct)
		{
			if (readDbNumber != dbNumber || Interlocked.Increment(ref _reads) != heldRead)
			{
				return Task.CompletedTask;
			}

			HeldToken = ct;
			_reached.SetResult();

			return _release.Task;
		}

		public void Release()
		{
			_release.SetResult();
		}

		public void Fail(Exception exception)
		{
			_release.SetException(exception);
		}
	}
}
