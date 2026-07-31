using System.Globalization;
using System.Linq;

using FluentResults;

using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes.Analysis.Warnings;
using SemiStep.Core.Recipes.Clipboard.Errors;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Formulas.Errors;
using SemiStep.Core.Recipes.Import.Errors;
using SemiStep.Core.Recipes.Import.Warnings;

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
			ConnectionLostError => Resources.ErrorConnectionLost,
			OwnedByAnotherInstanceError error => Format(
				Resources.ErrorOwnedByAnotherInstance,
				error.Holder.UserName,
				error.Holder.AcquiredUtc),
			FormulaComputationFailedError error => Format(Resources.ErrorFormulaComputationFailed, error.Target, Localize(error.Inner)),
			AtStepError error => Format(Resources.AtStepFormat, error.StepNumber, Localize(error.Inner)),
			AtColumnError error => Format(Resources.AtColumnFormat, error.ColumnKey, Localize(error.Inner)),
			AtRowError error => Format(Resources.AtRowFormat, error.RowNumber, Localize(error.Inner)),
			ClipboardParseFailedError => Resources.ErrorClipboardParseFailed,
			ColumnCountMismatchError error => Format(
				Resources.ErrorColumnCountMismatch, error.RowNumber, error.Expected, error.Actual),
			NoValidStepsError => Resources.ErrorNoValidSteps,
			ActionColumnNotFoundError => Resources.ErrorActionColumnNotFound,
			ActionColumnEmptyError => Resources.ErrorActionColumnEmpty,
			ActionValueNotIntegerError error => Format(Resources.ErrorActionValueNotInteger, error.RawAction),
			CsvBodyEmptyError => Resources.ErrorCsvBodyEmpty,
			CsvHeaderMismatchError error => Format(Resources.ErrorCsvHeaderMismatch, error.Expected, error.Actual),
			RecipeFileNotFoundError error => Format(Resources.ErrorRecipeFileNotFound, error.FilePath),
			RecipeLoadFailedError error => Format(Resources.ErrorRecipeLoadFailed, error.FilePath),
			RecipeSaveFailedError error => Format(Resources.ErrorRecipeSaveFailed, error.FilePath),
			PropertyValueTypeMismatchError error => Format(
				Resources.ErrorPropertyValueTypeMismatch, error.ExpectedType, error.ActualType, error.Id),
			UnsupportedPropertySystemTypeError error => Format(
				Resources.ErrorUnsupportedPropertySystemType, error.SystemType),
			GroupValueNotIntegerError error => Format(
				Resources.ErrorGroupValueNotInteger, error.ActualType),
			ValueBelowMinimumError error => Format(
				Resources.ErrorValueBelowMinimum, error.Value, error.Min, error.Id),
			ValueAboveMaximumError error => Format(
				Resources.ErrorValueAboveMaximum, error.Value, error.Max, error.Id),
			StringContainsNulError error => Format(
				Resources.ErrorStringContainsNul, error.Id),
			StringTooLongError error => Format(
				Resources.ErrorStringTooLong, error.Length, error.Max, error.Id),
			ActionByIdNotFoundError error => Format(
				Resources.ErrorActionByIdNotFound, error.Id),
			ActionByNameNotFoundError error => Format(
				Resources.ErrorActionByNameNotFound, error.Name),
			PropertyNotFoundError error => Format(
				Resources.ErrorPropertyNotFound, error.PropertyTypeId),
			ColumnNotFoundError error => Format(
				Resources.ErrorColumnNotFound, error.Key),
			GroupNotFoundError error => Format(
				Resources.ErrorGroupNotFound, error.GroupId),
			ValueNotInGroupError error => Format(
				Resources.ErrorValueNotInGroup, error.Key, error.GroupId),
			NoStateToUndoError => Resources.ErrorNoStateToUndo,
			NoStateToRedoError => Resources.ErrorNoStateToRedo,
			InsertIndexOutOfRangeError error => Format(
				Resources.ErrorInsertIndexOutOfRange, error.Index, error.StepCount),
			StepIndexOutOfRangeError error => Format(
				Resources.ErrorStepIndexOutOfRange, error.Index, error.StepCount),
			PropertyValueParseError error => Format(
				Resources.ErrorPropertyValueParse, error.RawValue, error.TargetType),
			MaxLoopNestingDepthExceededError error => Format(
				Resources.ErrorMaxLoopNestingDepthExceeded, error.MaxAllowed, error.ActualDepth),
			IterationCountUnsupportedTypeError error => Format(
				Resources.ErrorIterationCountUnsupportedType, error.Type, error.ActionKey),
			UnmatchedEndForWarning warning => Format(Resources.WarningUnmatchedEndFor, warning.StepIndex),
			UnclosedForLoopWarning warning => Format(Resources.WarningUnclosedForLoop, warning.StartIndex),
			RowCountMismatchWarning warning => Format(
				Resources.WarningRowCountMismatch, warning.FilePath, warning.MetadataRows, warning.ActualRows),
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
