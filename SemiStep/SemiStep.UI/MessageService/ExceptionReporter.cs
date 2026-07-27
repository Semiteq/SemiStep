using System;

using Microsoft.Extensions.Logging;

namespace SemiStep.UI.MessageService;

internal static class ExceptionReporter
{
	public static void ReportAndLog(MessagePanelViewModel panel, ILogger logger, string context, Exception exception)
	{
		logger.LogError(exception, "{Context}", context);
		panel.ReportError($"{context}: {exception.Message}");
	}
}
