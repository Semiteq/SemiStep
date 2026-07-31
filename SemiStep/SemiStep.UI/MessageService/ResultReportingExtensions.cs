using System.Collections.Generic;
using System.Linq;

using FluentResults;

using SemiStep.Core.Shared;

using SemiStep.UI.Localization;

namespace SemiStep.UI.MessageService;

public static class ResultReportingExtensions
{
	public static string FormatErrors(this IResultBase result)
	{
		return Join(result.Errors.Select(error => error.Message));
	}

	public static void ReportFailure(this MessagePanelViewModel panel, IResultBase result, string? context = null)
	{
		var message = Join(result.Errors.Select(ReasonLocalizer.Localize));
		panel.ReportError(context is null ? message : $"{context}: {message}");
	}

	public static void ReportFailure(this MessagePanelViewModel panel, IError error)
	{
		panel.ReportError(ReasonLocalizer.Localize(error));
	}

	public static void ReportWarnings(this MessagePanelViewModel panel, IResultBase result)
	{
		var message = Join(result.Successes.OfType<Warning>().Select(ReasonLocalizer.Localize));
		if (message.Length > 0)
		{
			panel.ReportWarning(message);
		}
	}

	private static string Join(IEnumerable<string> parts)
	{
		return string.Join("; ", parts);
	}
}
