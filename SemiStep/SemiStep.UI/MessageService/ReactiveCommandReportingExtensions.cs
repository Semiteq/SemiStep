using System;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.UI.Localization;

namespace SemiStep.UI.MessageService;

public static class ReactiveCommandReportingExtensions
{
	public static IDisposable ReportThrownExceptions<TParam, TResult>(
		this ReactiveCommand<TParam, TResult> command,
		MessagePanelViewModel panel,
		ILogger logger,
		LocalizedText context)
	{
		ArgumentNullException.ThrowIfNull(command);
		ArgumentNullException.ThrowIfNull(panel);
		ArgumentNullException.ThrowIfNull(logger);

		return command.ThrownExceptions.Subscribe(ex => ExceptionReporter.ReportAndLog(panel, logger, context, ex));
	}
}
