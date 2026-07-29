using System;

using Microsoft.Extensions.Logging;

using SemiStep.UI.Localization;

namespace SemiStep.UI.MessageService;

internal static class ExceptionReporter
{
	public static void ReportAndLog(MessagePanelViewModel panel, ILogger logger, LocalizedText context, Exception exception)
	{
		logger.LogError(exception, "{Context}", context.Invariant);
		panel.ReportError($"{context.Localized}: {exception.Message}");
	}
}
