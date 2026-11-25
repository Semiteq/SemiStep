using System.Reactive.Subjects;

using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;

namespace SemiStep.Tests.S7.Helpers;

internal sealed class StubS7ServiceForSync(bool connected) : IS7Connection, IS7ExecutionStream
{
	private readonly Subject<PlcExecutionInfo> _executionState = new();

	public bool IsConnected { get; private set; } = connected;

	public bool IsRecipeActive => false;

	public IObservable<PlcExecutionInfo> ExecutionState => _executionState;

	public DateTimeOffset? LastExchangeTime => null;

	public event Action<PlcConnectionState>? StateChanged
	{
		add { }
		remove { }
	}

	public void SetConnected(bool connected)
	{
		IsConnected = connected;
	}

	public void PushExecutionState(PlcExecutionInfo info)
	{
		_executionState.OnNext(info);
	}

	public Task ConnectAsync(PlcConnectionSettings settings)
	{
		return Task.CompletedTask;
	}

	public Task DisconnectAsync()
	{
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		_executionState.Dispose();
		return ValueTask.CompletedTask;
	}
}
