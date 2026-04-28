using System.Linq;
using System.Reactive.Subjects;

using FluentResults;

using S7.Protocol;

using Serilog;

using TypesShared.Plc;

namespace S7.Sync;

internal sealed class PlcExecutionMonitor(
	PlcTransactionExecutor transactionExecutor,
	PlcProtocolSettings protocolSettings,
	Action onConnectionLost)
	: IDisposable
{
	private static readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(5);

	private readonly Subject<PlcExecutionInfo> _subject = new();

	private volatile PlcExecutionInfo _lastKnown = PlcExecutionInfo.Empty;
	private CancellationTokenSource? _pollCts;
	private Task? _pollTask;

	public IObservable<PlcExecutionInfo> State => _subject;

	public PlcExecutionInfo LastKnown => _lastKnown;

	public void Start(CancellationToken externalCancellationToken = default)
	{
		Stop();

		_pollCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
		_pollTask = PollLoopAsync(_pollCts.Token);
	}

	public void Stop()
	{
		var taskToWait = CancelAndDetachPollTask();

		// Skip the wait if Stop() is being called from within the poll loop itself
		// (e.g. via onConnectionLost callback), to avoid deadlock.
		if (taskToWait is not null && taskToWait.Id != Task.CurrentId)
		{
			taskToWait.Wait(_stopTimeout);
		}

		PublishAndTrack(PlcExecutionInfo.Empty);
	}

	public async Task StopAsync()
	{
		var taskToWait = CancelAndDetachPollTask();

		if (taskToWait is not null)
		{
			try
			{
				await taskToWait.WaitAsync(_stopTimeout);
			}
			catch (TimeoutException)
			{
				Log.Warning("Execution monitor poll loop did not stop within 5 seconds");
			}
		}

		PublishAndTrack(PlcExecutionInfo.Empty);
	}

	public void Dispose()
	{
		Stop();
		_subject.OnCompleted();
		_subject.Dispose();
	}

	private Task? CancelAndDetachPollTask()
	{
		_pollCts?.Cancel();
		_pollCts?.Dispose();
		_pollCts = null;

		var taskToWait = _pollTask;
		_pollTask = null;

		return taskToWait;
	}

	private void PublishAndTrack(PlcExecutionInfo info)
	{
		_lastKnown = info;
		_subject.OnNext(info);
	}

	private async Task PollLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(protocolSettings.PollingIntervalMs, ct);
				var result = await transactionExecutor.ReadExecutionStateAsync(ct);

				if (result.IsFailed)
				{
					if (result.Errors.OfType<NotConnectedError>().Any())
					{
						Log.Debug("Execution monitor stopping: PLC not connected");

						if (!(_pollCts?.IsCancellationRequested ?? true))
						{
							onConnectionLost();
						}

						return;
					}

					Log.Warning("Execution monitor poll error: {Message}", result.Errors[0].Message);
					continue;
				}

				PublishAndTrack(result.Value);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}
	}
}
