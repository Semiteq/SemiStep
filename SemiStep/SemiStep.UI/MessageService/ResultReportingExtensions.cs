using System.Collections.Generic;
using System.Linq;

using FluentResults;

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

	private static string Join(IEnumerable<string> parts)
	{
		return string.Join("; ", parts);
	}
}
