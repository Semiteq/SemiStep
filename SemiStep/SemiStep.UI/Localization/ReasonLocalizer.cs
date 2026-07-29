using System.Globalization;
using System.Linq;

using FluentResults;

using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Formulas.Errors;

namespace SemiStep.UI.Localization;

public static class ReasonLocalizer
{
	public static string Localize(IReason reason)
	{
		return TryLocalize(reason) ?? reason.Message;
	}

	private static string? TryLocalize(IReason reason)
	{
		var text = reason switch
		{
			OwnedByAnotherInstanceError error => Format(
				Resources.ErrorOwnedByAnotherInstance,
				error.Holder.UserName,
				error.Holder.AcquiredUtc),
			FormulaComputationFailedError error => Format(Resources.ErrorFormulaComputationFailed, error.Target, error.Reason),
			AtStepError error => Format(Resources.AtStepFormat, error.StepNumber, Localize(error.Inner)),
			AtColumnError error => Format(Resources.AtColumnFormat, error.ColumnKey, Localize(error.Inner)),
			_ => null
		};

		if (text is { Length: > 0 })
		{
			return text;
		}

		if (reason is IError parent)
		{
			foreach (var cause in parent.Reasons.OfType<IError>())
			{
				if (TryLocalize(cause) is { } localized)
				{
					return localized;
				}
			}
		}

		return null;
	}

	private static string Format(string template, params object[] args)
	{
		return string.Format(Resources.Culture ?? CultureInfo.CurrentUICulture, template, args);
	}
}
