using System.Text;

using Avalonia.Logging;

using Xunit.Sdk;

namespace SemiStep.Tests.UI.Helpers;

// Installs a collecting Avalonia log sink for the lifetime of the scope and restores the previous
// Logger.Sink (including null) on dispose, so a headless test can assert it produced zero binding
// errors. Logger.Sink is a single global slot; assembly parallelization is disabled, so capture and
// restore around the guarded interaction is enough to keep it from leaking between tests.
public sealed class BindingErrorGuard : IDisposable
{
	private readonly ILogSink? _previousSink;
	private readonly List<CapturedLogEvent> _events = new();
	private bool _disposed;

	public BindingErrorGuard()
	{
		_previousSink = Logger.Sink;
		Logger.Sink = new CollectingSink(_events);
	}

	public IReadOnlyList<string> BindingErrors =>
		_events.Where(static logEvent => logEvent.Area == LogArea.Binding)
			.Select(static logEvent => logEvent.Message)
			.ToList();

	public int BindingErrorCount => BindingErrors.Count;

	public void AssertNoBindingErrors()
	{
		var bindingErrors = BindingErrors;
		if (bindingErrors.Count == 0)
		{
			return;
		}

		var details = string.Join(Environment.NewLine, bindingErrors.Select(static message => "  - " + message));
		throw new XunitException(
			$"Expected zero Avalonia binding errors, but {bindingErrors.Count} were recorded:{Environment.NewLine}{details}");
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		Logger.Sink = _previousSink;
	}

	private static string RenderMessage(string messageTemplate, object?[] propertyValues)
	{
		if (propertyValues.Length == 0)
		{
			return messageTemplate;
		}

		var builder = new StringBuilder(messageTemplate.Length);
		var valueIndex = 0;
		var position = 0;
		while (position < messageTemplate.Length)
		{
			var character = messageTemplate[position];
			if (character == '{')
			{
				var closing = messageTemplate.IndexOf('}', position);
				if (closing > position)
				{
					var value = valueIndex < propertyValues.Length ? propertyValues[valueIndex] : null;
					builder.Append(value?.ToString() ?? "null");
					valueIndex++;
					position = closing + 1;
					continue;
				}
			}

			builder.Append(character);
			position++;
		}

		return builder.ToString();
	}

	public sealed record CapturedLogEvent(string Area, string Message);

	private sealed class CollectingSink : ILogSink
	{
		private readonly List<CapturedLogEvent> _events;

		public CollectingSink(List<CapturedLogEvent> events)
		{
			_events = events;
		}

		public bool IsEnabled(LogEventLevel level, string area)
		{
			return true;
		}

		public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
		{
			Log(level, area, source, messageTemplate, Array.Empty<object?>());
		}

		public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
		{
			_events.Add(new CapturedLogEvent(area, RenderMessage(messageTemplate, propertyValues)));
		}
	}
}
