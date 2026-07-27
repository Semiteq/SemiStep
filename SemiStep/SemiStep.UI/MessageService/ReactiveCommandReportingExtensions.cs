using System;

using Microsoft.Extensions.Logging;

using ReactiveUI;

namespace SemiStep.UI.MessageService;

public static class ReactiveCommandReportingExtensions
{
	public static IDisposable ReportThrownExceptions<TParam, TResult>(
		this ReactiveCommand<TParam, TResult> command,
		MessagePanelViewModel panel,
		ILogger logger,
		string context)
	{
		ArgumentNullException.ThrowIfNull(command);
		ArgumentNullException.ThrowIfNull(panel);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(context);

		return command.ThrownExceptions.Subscribe(ex => ExceptionReporter.ReportAndLog(panel, logger, context, ex));
	}
}
