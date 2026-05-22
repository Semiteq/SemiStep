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

	public bool IsConnected => true;

	public bool IsRecipeActive => false;

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

	/// <summary>Raises <see cref="StateChanged"/> with the given state.</summary>
	public void RaiseStateChanged(PlcConnectionState state)
	{
		StateChanged?.Invoke(state);
	}

	public Task ConnectAsync(PlcConnectionSettings settings)
	{
		return Task.CompletedTask;
	}

	public Task DisconnectAsync()
	{
		return Task.CompletedTask;
	}

	public Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync()
	{
		if (ManagingAreaToReturn is not null)
		{
			return Task.FromResult(Result.Ok(ManagingAreaToReturn));
		}

		return Task.FromResult(Result.Fail<PlcManagingAreaState>("Not connected"));
	}

	public Task<Result<Recipe>> ReadRecipeFromPlcAsync()
	{
		if (RecipeToReturn is not null)
		{
			return Task.FromResult(Result.Ok(RecipeToReturn));
		}

		return Task.FromResult(Result.Fail<Recipe>("Not connected"));
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
