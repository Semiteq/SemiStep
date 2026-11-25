using Serilog.Core;
using Serilog.Events;

namespace SemiStep.Tests.Helpers;

public sealed class CapturingSink : ILogEventSink
{
	private readonly Lock _lock = new();
	private readonly List<LogEvent> _events = [];

	public IReadOnlyList<LogEvent> Events
	{
		get
		{
			lock (_lock)
			{
				return [.. _events];
			}
		}
	}

	public void Emit(LogEvent logEvent)
	{
		lock (_lock)
		{
			_events.Add(logEvent);
		}
	}
}
