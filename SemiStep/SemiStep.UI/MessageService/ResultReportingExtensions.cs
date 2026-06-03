using System.Linq;

using FluentResults;

namespace SemiStep.UI.MessageService;

public static class ResultReportingExtensions
{
	public static string FormatErrors(this IResultBase result)
	{
		return string.Join("; ", result.Errors.Select(error => error.Message));
	}

	public static void ReportFailure(this MessagePanelViewModel panel, IResultBase result, string? context = null)
	{
		var message = result.FormatErrors();
		panel.ReportError(context is null ? message : $"{context}: {message}");
	}
}
