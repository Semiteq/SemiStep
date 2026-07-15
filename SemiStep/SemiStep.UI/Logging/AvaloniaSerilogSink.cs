using System.Collections.Concurrent;

using Avalonia.Logging;

using Serilog;

using AvaloniaLevel = Avalonia.Logging.LogEventLevel;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace SemiStep.UI.Logging;

public sealed class AvaloniaSerilogSink : ILogSink
{
	private const int FullLogCount = 20;
	private const int SampleInterval = 500;

	private readonly AvaloniaLevel _minimumLevel;

	// The key set is trusted finite: Avalonia log templates are constant strings from a fixed set of
	// call sites, so this never grows unbounded and needs no eviction.
	private readonly ConcurrentDictionary<(string Area, string Template), int> _occurrences = new();

	public AvaloniaSerilogSink(AvaloniaLevel minimumLevel)
	{
		_minimumLevel = minimumLevel;
	}

	public bool IsEnabled(AvaloniaLevel level, string area)
	{
		return level >= _minimumLevel;
	}

	public void Log(AvaloniaLevel level, string area, object? source, string messageTemplate)
	{
		Log(level, area, source, messageTemplate, Array.Empty<object?>());
	}

	public void Log(AvaloniaLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
	{
		if (level < _minimumLevel)
		{
			return;
		}

		var occurrence = _occurrences.AddOrUpdate((area, messageTemplate), 1, static (_, count) => count + 1);
		if (!ShouldEmit(occurrence))
		{
			return;
		}

		var logger = Serilog.Log.ForContext("SourceContext", "Avalonia." + area);
		if (occurrence > FullLogCount)
		{
			logger = logger.ForContext("Occurrence", occurrence);
		}

		logger.Write(MapLevel(level), messageTemplate, propertyValues);
	}

	private static bool ShouldEmit(int occurrence)
	{
		if (occurrence <= FullLogCount)
		{
			return true;
		}

		return occurrence % SampleInterval == 0;
	}

	private static SerilogLevel MapLevel(AvaloniaLevel level)
	{
		switch (level)
		{
			case AvaloniaLevel.Verbose:
				return SerilogLevel.Verbose;
			case AvaloniaLevel.Debug:
				return SerilogLevel.Debug;
			case AvaloniaLevel.Information:
				return SerilogLevel.Information;
			case AvaloniaLevel.Warning:
				return SerilogLevel.Warning;
			case AvaloniaLevel.Error:
				return SerilogLevel.Error;
			case AvaloniaLevel.Fatal:
				return SerilogLevel.Fatal;
			default:
				return SerilogLevel.Information;
		}
	}
}
