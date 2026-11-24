using System.Reactive.Subjects;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Plc.Sync;

internal sealed class PlcExecutionMonitor(
	PlcTransactionExecutor transactionExecutor,
	PlcProtocolSettings protocolSettings,
	Action onConnectionLost,
	ILogger<PlcExecutionMonitor> logger)
	: IDisposable
{
	private static readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(5);

	private readonly ILogger<PlcExecutionMonitor> _logger = logger;

	// Written by the poll loop (thread pool) and by Stop/StopAsync callers (keep-alive and reconnect
	// threads, UI disconnect).
	private readonly ISubject<PlcExecutionInfo> _subject = Subject.Synchronize(new Subject<PlcExecutionInfo>());

	// Guards _pollCts and _pollTask, which Start/Stop callers and the poll loop change from different threads.
	private readonly Lock _pollLock = new();

	private CancellationTokenSource? _pollCts;
	private Task? _pollTask;

	// Written by the poll loop and Stop/StopAsync callers, read by S7Service.IsRecipeActive (UI thread).
	private volatile PlcExecutionInfo _lastKnown = PlcExecutionInfo.Empty;

	public IObservable<PlcExecutionInfo> State => _subject;

	public PlcExecutionInfo LastKnown => _lastKnown;

	public void Start(CancellationToken externalCancellationToken = default)
	{
		Stop();

		lock (_pollLock)
		{
			// A concurrent Start can create a loop between Stop() above and this section.
			_ = CancelAndDetachPollTask();

			var pollCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
			// Taken now: a stop can dispose the source before the loop starts, and .Token would then throw.
			var token = pollCts.Token;
			_pollCts = pollCts;
			_pollTask = Task.Run(() => PollLoopAsync(token), CancellationToken.None);
		}
	}

	public void Stop()
	{
		Task? taskToWait;
		lock (_pollLock)
		{
			taskToWait = CancelAndDetachPollTask();
		}

		if (taskToWait is not null && !taskToWait.Wait(_stopTimeout))
		{
			LogStopTimeout();
		}

		PublishAndTrack(PlcExecutionInfo.Empty);
	}

	public async Task StopAsync()
	{
		Task? taskToWait;
		lock (_pollLock)
		{
			taskToWait = CancelAndDetachPollTask();
		}

		if (taskToWait is not null)
		{
			try
			{
				await taskToWait.WaitAsync(_stopTimeout);
			}
			catch (TimeoutException)
			{
				LogStopTimeout();
			}
		}

		PublishAndTrack(PlcExecutionInfo.Empty);
	}

	public void Dispose()
	{
		Stop();
		_subject.OnCompleted();
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

	private void LogStopTimeout()
	{
		_logger.LogWarning("Execution monitor poll loop did not stop within 5 seconds");
	}

	private void PublishAndTrack(PlcExecutionInfo info)
	{
		_lastKnown = info;
		_subject.OnNext(info);
	}

	private bool TryDetachCurrentLoop(CancellationToken ct)
	{
		lock (_pollLock)
		{
			if (ct.IsCancellationRequested)
			{
				return false;
			}

			_pollCts?.Dispose();
			_pollCts = null;
			_pollTask = null;

			return true;
		}
	}

	private async Task PollLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(protocolSettings.PollingIntervalMs, ct);
				var result = await transactionExecutor.ReadExecutionStateAsync(ct);

				if (ct.IsCancellationRequested)
				{
					return;
				}

				if (result.IsFailed)
				{
					if (result.Errors.OfType<NotConnectedError>().Any())
					{
						_logger.LogDebug("Execution monitor stopping: PLC not connected");

						if (TryDetachCurrentLoop(ct))
						{
							onConnectionLost();
						}

						return;
					}

					if (!result.Errors.OfType<PlcCommandFailedError>().Any())
					{
						_logger.LogWarning(
							"Execution monitor poll error: {Message}", result.Errors[0].Message);
					}

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
