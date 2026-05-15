using Microsoft.Extensions.Logging;

namespace SemiStep.Tests.Helpers;

public sealed class RecordingLogger<T> : ILogger<T>
{
	private readonly List<LogEntry> _entries = new();

	public IReadOnlyList<LogEntry> Entries => _entries;

	public IDisposable BeginScope<TState>(TState state) where TState : notnull
	{
		return NullScope.Instance;
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return true;
	}

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		_entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
	}

	public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
		}
	}
}
