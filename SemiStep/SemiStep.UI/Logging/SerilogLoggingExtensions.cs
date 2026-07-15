using Avalonia;
using Avalonia.Logging;

using AvaloniaLevel = Avalonia.Logging.LogEventLevel;

namespace SemiStep.UI.Logging;

public static class SerilogLoggingExtensions
{
	public static AppBuilder LogToSerilog(this AppBuilder builder, AvaloniaLevel level = AvaloniaLevel.Warning)
	{
		Logger.Sink = new AvaloniaSerilogSink(level);
		return builder;
	}
}
