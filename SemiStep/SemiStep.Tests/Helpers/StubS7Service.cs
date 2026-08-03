using System.Reactive.Subjects;

using FluentResults;

using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Tests.Helpers;

public sealed class StubS7Service : IS7Connection, IS7Reader, IS7ExecutionStream, IDisposable
{
	private readonly Subject<PlcExecutionInfo> _executionState = new();
	private bool _disposed;

	public bool IsConnected { get; set; } = true;

	public bool IsRecipeActive => false;

	/// <summary>
	/// When true, <see cref="ConnectAsync"/> throws so that callers such as
	/// <c>PlcLifecycleManager.EnableSync</c> return a failed result.
	/// </summary>
	public bool ConnectShouldFail { get; set; }

	/// <summary>
	/// When true, <see cref="ConnectAsync"/> raises <see cref="StateChanged"/> with
	/// <see cref="PlcConnectionState.Connected"/> synchronously before returning, mirroring the real
	/// <c>S7Service</c> which publishes <c>Connected</c> inside <c>ConnectAsync</c>. This exercises the
	/// ordering where reconnect reconciliation is triggered during the connect call, before the
	/// <c>EnableSync</c> version handshake completes.
	/// </summary>
	public bool RaiseConnectedDuringConnect { get; set; }

	public IObservable<PlcExecutionInfo> ExecutionState => _executionState;

	public void PushExecutionState(PlcExecutionInfo info)
	{
		_executionState.OnNext(info);
	}

	public event Action<PlcConnectionState>? StateChanged;

	/// <summary>
	/// When set, <see cref="ReadManagingAreaAsync"/> returns this value instead of a failure.
	/// </summary>
	public PlcManagingAreaState? ManagingAreaToReturn { get; set; }

	/// <summary>
	/// When set, <see cref="ReadRecipeFromPlcAsync"/> returns this recipe instead of a failure.
	/// </summary>
	public Recipe? RecipeToReturn { get; set; }

	/// <summary>
	/// The result returned by <see cref="ReadProtocolVersionAsync"/>. Defaults to the matching
	/// protocol version so the <c>EnableSync</c> handshake succeeds; a mismatch test can override it.
	/// </summary>
	public Result<int> ProtocolVersionToReturn { get; set; } = Result.Ok(1);

	/// <summary>
	/// When true, <see cref="ReadProtocolVersionAsync"/> throws a <see cref="TaskCanceledException"/>,
	/// mirroring an S7.Net socket/read timeout that surfaces as an <see cref="OperationCanceledException"/>
	/// derivative. This exercises the <c>EnableSync</c> rollback path for a handshake that throws rather
	/// than returning a failed result.
	/// </summary>
	public bool ProtocolVersionReadShouldThrowCanceled { get; set; }

	/// <summary>
	/// When set, <see cref="ReadManagingAreaAsync"/> awaits this gate (observing the token) before
	/// returning. Lets a test suspend reconnect reconciliation at the managing-area read and cancel
	/// the token mid-wait to prove the read is interrupted rather than polled.
	/// </summary>
	public ReadGate? ManagingAreaReadGate { get; set; }

	/// <summary>
	/// When set, <see cref="ReadProtocolVersionAsync"/> awaits this gate (observing the token) before
	/// returning. Lets a test suspend the <c>EnableSync</c> handshake at the version read and cancel
	/// the token mid-wait to exercise the genuine-cancel branch.
	/// </summary>
	public ReadGate? ProtocolVersionReadGate { get; set; }

	/// <summary>Raises <see cref="StateChanged"/> with the given state.</summary>
	public void RaiseStateChanged(PlcConnectionState state)
	{
		StateChanged?.Invoke(state);
	}

	/// <summary>Number of times <see cref="ConnectAsync"/> was called.</summary>
	public int ConnectCallCount { get; private set; }

	public Task ConnectAsync(PlcConnectionSettings settings)
	{
		ConnectCallCount++;

		if (ConnectShouldFail)
		{
			throw new InvalidOperationException("Stub PLC connection failure");
		}

		if (RaiseConnectedDuringConnect)
		{
			StateChanged?.Invoke(PlcConnectionState.Connected);
		}

		return Task.CompletedTask;
	}

	/// <summary>Number of times <see cref="DisconnectAsync"/> was called.</summary>
	public int DisconnectCallCount { get; private set; }

	public Task DisconnectAsync()
	{
		DisconnectCallCount++;
		return Task.CompletedTask;
	}

	/// <summary>Number of times <see cref="ReadManagingAreaAsync"/> was called.</summary>
	public int ReadManagingAreaCallCount { get; private set; }

	/// <summary>Number of times <see cref="ReadRecipeFromPlcAsync"/> was called.</summary>
	public int ReadRecipeFromPlcCallCount { get; private set; }

	private readonly TaskCompletionSource _recipeReadReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>Completes the first time <see cref="ReadRecipeFromPlcAsync"/> is entered.</summary>
	public Task RecipeReadReached => _recipeReadReached.Task;

	public Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		ReadManagingAreaCallCount++;

		if (ManagingAreaReadGate is not null)
		{
			return GateThenReadManagingAreaAsync(ManagingAreaReadGate, ct);
		}

		return Task.FromResult(BuildManagingAreaResult());
	}

	private async Task<Result<PlcManagingAreaState>> GateThenReadManagingAreaAsync(ReadGate gate, CancellationToken ct)
	{
		await gate.WaitAsync(ct);
		return BuildManagingAreaResult();
	}

	private Result<PlcManagingAreaState> BuildManagingAreaResult()
	{
		if (ManagingAreaToReturn is not null)
		{
			return Result.Ok(ManagingAreaToReturn);
		}

		return Result.Fail<PlcManagingAreaState>("Not connected");
	}

	public Task<Result<Recipe>> ReadRecipeFromPlcAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		ReadRecipeFromPlcCallCount++;
		_recipeReadReached.TrySetResult();

		if (RecipeToReturn is not null)
		{
			return Task.FromResult(Result.Ok(RecipeToReturn));
		}

		return Task.FromResult(Result.Fail<Recipe>("Not connected"));
	}

	public Task<Result<int>> ReadProtocolVersionAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		if (ProtocolVersionReadShouldThrowCanceled)
		{
			throw new TaskCanceledException("Stub protocol version read timed out");
		}

		if (ProtocolVersionReadGate is not null)
		{
			return GateThenReadProtocolVersionAsync(ProtocolVersionReadGate, ct);
		}

		return Task.FromResult(ProtocolVersionToReturn);
	}

	private async Task<Result<int>> GateThenReadProtocolVersionAsync(ReadGate gate, CancellationToken ct)
	{
		await gate.WaitAsync(ct);
		return ProtocolVersionToReturn;
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_executionState.Dispose();
	}
}
