using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Globalization;
using System.Resources;

#nullable enable

namespace SemiStep.UI.Localization;

[GeneratedCode("SemiStep.Localization", "1.0.0.0")]
[DebuggerNonUserCode]
public class Resources
{
	private static ResourceManager? resourceManager;

	private static CultureInfo? resourceCulture;

	public static ResourceManager ResourceManager
	{
		get
		{
			if (resourceManager is null)
			{
				resourceManager = new ResourceManager("SemiStep.UI.Localization.Resources", typeof(Resources).Assembly);
			}

			return resourceManager;
		}
	}

	public static CultureInfo? Culture
	{
		get => resourceCulture;
		set => resourceCulture = value;
	}

	public static string MenuFile => ResourceManager.GetString("MenuFile", resourceCulture) ?? string.Empty;

	public static string MenuFileNewRecipe => ResourceManager.GetString("MenuFileNewRecipe", resourceCulture) ?? string.Empty;

	public static string MenuFileOpenRecipe => ResourceManager.GetString("MenuFileOpenRecipe", resourceCulture) ?? string.Empty;

	public static string MenuFileSaveRecipe => ResourceManager.GetString("MenuFileSaveRecipe", resourceCulture) ?? string.Empty;

	public static string MenuFileSaveRecipeAs => ResourceManager.GetString("MenuFileSaveRecipeAs", resourceCulture) ?? string.Empty;

	public static string MenuFileExit => ResourceManager.GetString("MenuFileExit", resourceCulture) ?? string.Empty;

	public static string MenuEdit => ResourceManager.GetString("MenuEdit", resourceCulture) ?? string.Empty;

	public static string MenuEditAddStep => ResourceManager.GetString("MenuEditAddStep", resourceCulture) ?? string.Empty;

	public static string MenuEditDeleteStep => ResourceManager.GetString("MenuEditDeleteStep", resourceCulture) ?? string.Empty;

	public static string MenuEditCopySteps => ResourceManager.GetString("MenuEditCopySteps", resourceCulture) ?? string.Empty;

	public static string MenuEditCutSteps => ResourceManager.GetString("MenuEditCutSteps", resourceCulture) ?? string.Empty;

	public static string MenuEditPasteSteps => ResourceManager.GetString("MenuEditPasteSteps", resourceCulture) ?? string.Empty;

	public static string MenuEditUndo => ResourceManager.GetString("MenuEditUndo", resourceCulture) ?? string.Empty;

	public static string MenuEditRedo => ResourceManager.GetString("MenuEditRedo", resourceCulture) ?? string.Empty;

	public static string MenuView => ResourceManager.GetString("MenuView", resourceCulture) ?? string.Empty;

	public static string MenuViewToolbar => ResourceManager.GetString("MenuViewToolbar", resourceCulture) ?? string.Empty;

	public static string MenuViewNotificationLog => ResourceManager.GetString("MenuViewNotificationLog", resourceCulture) ?? string.Empty;

	public static string MenuViewTransposedGrid => ResourceManager.GetString("MenuViewTransposedGrid", resourceCulture) ?? string.Empty;

	public static string MenuViewGridStyleSettings => ResourceManager.GetString("MenuViewGridStyleSettings", resourceCulture) ?? string.Empty;

	public static string MenuHelp => ResourceManager.GetString("MenuHelp", resourceCulture) ?? string.Empty;

	public static string MenuHelpAbout => ResourceManager.GetString("MenuHelpAbout", resourceCulture) ?? string.Empty;

	public static string ContextAddStep => ResourceManager.GetString("ContextAddStep", resourceCulture) ?? string.Empty;

	public static string ContextDeleteStep => ResourceManager.GetString("ContextDeleteStep", resourceCulture) ?? string.Empty;

	public static string ContextCopyStep => ResourceManager.GetString("ContextCopyStep", resourceCulture) ?? string.Empty;

	public static string ContextCutStep => ResourceManager.GetString("ContextCutStep", resourceCulture) ?? string.Empty;

	public static string ContextPasteStep => ResourceManager.GetString("ContextPasteStep", resourceCulture) ?? string.Empty;

	public static string ToolbarAddStep => ResourceManager.GetString("ToolbarAddStep", resourceCulture) ?? string.Empty;

	public static string ToolbarDeleteStep => ResourceManager.GetString("ToolbarDeleteStep", resourceCulture) ?? string.Empty;

	public static string ToolbarCopyStep => ResourceManager.GetString("ToolbarCopyStep", resourceCulture) ?? string.Empty;

	public static string ToolbarCutStep => ResourceManager.GetString("ToolbarCutStep", resourceCulture) ?? string.Empty;

	public static string ToolbarPasteStep => ResourceManager.GetString("ToolbarPasteStep", resourceCulture) ?? string.Empty;

	public static string ToolbarUndo => ResourceManager.GetString("ToolbarUndo", resourceCulture) ?? string.Empty;

	public static string ToolbarRedo => ResourceManager.GetString("ToolbarRedo", resourceCulture) ?? string.Empty;

	public static string StatusSyncOn => ResourceManager.GetString("StatusSyncOn", resourceCulture) ?? string.Empty;

	public static string StatusSyncOff => ResourceManager.GetString("StatusSyncOff", resourceCulture) ?? string.Empty;

	public static string StatusConnecting => ResourceManager.GetString("StatusConnecting", resourceCulture) ?? string.Empty;

	public static string StatusStepLabel => ResourceManager.GetString("StatusStepLabel", resourceCulture) ?? string.Empty;

	public static string StatusRecipeLabel => ResourceManager.GetString("StatusRecipeLabel", resourceCulture) ?? string.Empty;

	public static string StatusForLabel => ResourceManager.GetString("StatusForLabel", resourceCulture) ?? string.Empty;

	public static string MessagePanelClose => ResourceManager.GetString("MessagePanelClose", resourceCulture) ?? string.Empty;

	public static string DialogMessageTitle => ResourceManager.GetString("DialogMessageTitle", resourceCulture) ?? string.Empty;

	public static string DialogOk => ResourceManager.GetString("DialogOk", resourceCulture) ?? string.Empty;

	public static string DialogExitTitle => ResourceManager.GetString("DialogExitTitle", resourceCulture) ?? string.Empty;

	public static string DialogExitMessage => ResourceManager.GetString("DialogExitMessage", resourceCulture) ?? string.Empty;

	public static string DialogSave => ResourceManager.GetString("DialogSave", resourceCulture) ?? string.Empty;

	public static string DialogDontSave => ResourceManager.GetString("DialogDontSave", resourceCulture) ?? string.Empty;

	public static string DialogCancel => ResourceManager.GetString("DialogCancel", resourceCulture) ?? string.Empty;

	public static string PlcConflictTitle => ResourceManager.GetString("PlcConflictTitle", resourceCulture) ?? string.Empty;

	public static string PlcConflictLine1 => ResourceManager.GetString("PlcConflictLine1", resourceCulture) ?? string.Empty;

	public static string PlcConflictQuestion => ResourceManager.GetString("PlcConflictQuestion", resourceCulture) ?? string.Empty;

	public static string PlcConflictKeepLocal => ResourceManager.GetString("PlcConflictKeepLocal", resourceCulture) ?? string.Empty;

	public static string PlcConflictLoadFromPlc => ResourceManager.GetString("PlcConflictLoadFromPlc", resourceCulture) ?? string.Empty;

	public static string StatusIdle => ResourceManager.GetString("StatusIdle", resourceCulture) ?? string.Empty;

	public static string StatusSyncing => ResourceManager.GetString("StatusSyncing", resourceCulture) ?? string.Empty;

	public static string StatusSynced => ResourceManager.GetString("StatusSynced", resourceCulture) ?? string.Empty;

	public static string StatusFailed => ResourceManager.GetString("StatusFailed", resourceCulture) ?? string.Empty;

	public static string LastSyncNever => ResourceManager.GetString("LastSyncNever", resourceCulture) ?? string.Empty;

	public static string LastSyncAgoFormat => ResourceManager.GetString("LastSyncAgoFormat", resourceCulture) ?? string.Empty;

	public static string LastSyncPrefix => ResourceManager.GetString("LastSyncPrefix", resourceCulture) ?? string.Empty;

	public static string WindowTitleNewRecipe => ResourceManager.GetString("WindowTitleNewRecipe", resourceCulture) ?? string.Empty;

	public static string PlcConflictLocalSteps => ResourceManager.GetString("PlcConflictLocalSteps", resourceCulture) ?? string.Empty;

	public static string PlcConflictPlcSteps => ResourceManager.GetString("PlcConflictPlcSteps", resourceCulture) ?? string.Empty;

	public static string MessagePanelErrorsFormat => ResourceManager.GetString("MessagePanelErrorsFormat", resourceCulture) ?? string.Empty;

	public static string MessagePanelWarningsFormat => ResourceManager.GetString("MessagePanelWarningsFormat", resourceCulture) ?? string.Empty;

	public static string EditorTitle => ResourceManager.GetString("EditorTitle", resourceCulture) ?? string.Empty;

	public static string EditorRecipeGrid => ResourceManager.GetString("EditorRecipeGrid", resourceCulture) ?? string.Empty;

	public static string EditorStatusBar => ResourceManager.GetString("EditorStatusBar", resourceCulture) ?? string.Empty;

	public static string EditorNotificationPanel => ResourceManager.GetString("EditorNotificationPanel", resourceCulture) ?? string.Empty;

	public static string EditorApplicationTheme => ResourceManager.GetString("EditorApplicationTheme", resourceCulture) ?? string.Empty;

	public static string EditorDimensions => ResourceManager.GetString("EditorDimensions", resourceCulture) ?? string.Empty;

	public static string EditorFonts => ResourceManager.GetString("EditorFonts", resourceCulture) ?? string.Empty;

	public static string EditorColors => ResourceManager.GetString("EditorColors", resourceCulture) ?? string.Empty;

	public static string EditorReadOnlyCells => ResourceManager.GetString("EditorReadOnlyCells", resourceCulture) ?? string.Empty;

	public static string EditorDisabledCells => ResourceManager.GetString("EditorDisabledCells", resourceCulture) ?? string.Empty;

	public static string EditorExecutionHighlight => ResourceManager.GetString("EditorExecutionHighlight", resourceCulture) ?? string.Empty;

	public static string EditorTypography => ResourceManager.GetString("EditorTypography", resourceCulture) ?? string.Empty;

	public static string EditorSharedColors => ResourceManager.GetString("EditorSharedColors", resourceCulture) ?? string.Empty;

	public static string EditorSize => ResourceManager.GetString("EditorSize", resourceCulture) ?? string.Empty;

	public static string EditorWeight => ResourceManager.GetString("EditorWeight", resourceCulture) ?? string.Empty;

	public static string EditorItalic => ResourceManager.GetString("EditorItalic", resourceCulture) ?? string.Empty;

	public static string EditorCurrent => ResourceManager.GetString("EditorCurrent", resourceCulture) ?? string.Empty;

	public static string EditorPast => ResourceManager.GetString("EditorPast", resourceCulture) ?? string.Empty;

	public static string EditorRowHeight => ResourceManager.GetString("EditorRowHeight", resourceCulture) ?? string.Empty;

	public static string EditorCellPaddingLeft => ResourceManager.GetString("EditorCellPaddingLeft", resourceCulture) ?? string.Empty;

	public static string EditorCellPaddingTop => ResourceManager.GetString("EditorCellPaddingTop", resourceCulture) ?? string.Empty;

	public static string EditorCellPaddingRight => ResourceManager.GetString("EditorCellPaddingRight", resourceCulture) ?? string.Empty;

	public static string EditorCellPaddingBottom => ResourceManager.GetString("EditorCellPaddingBottom", resourceCulture) ?? string.Empty;

	public static string EditorGridHeader => ResourceManager.GetString("EditorGridHeader", resourceCulture) ?? string.Empty;

	public static string EditorGridCell => ResourceManager.GetString("EditorGridCell", resourceCulture) ?? string.Empty;

	public static string EditorGridBackground => ResourceManager.GetString("EditorGridBackground", resourceCulture) ?? string.Empty;

	public static string EditorGridBorder => ResourceManager.GetString("EditorGridBorder", resourceCulture) ?? string.Empty;

	public static string EditorGridLine => ResourceManager.GetString("EditorGridLine", resourceCulture) ?? string.Empty;

	public static string EditorHeaderForeground => ResourceManager.GetString("EditorHeaderForeground", resourceCulture) ?? string.Empty;

	public static string EditorSelectionBackground => ResourceManager.GetString("EditorSelectionBackground", resourceCulture) ?? string.Empty;

	public static string EditorSelectionForeground => ResourceManager.GetString("EditorSelectionForeground", resourceCulture) ?? string.Empty;

	public static string EditorChanged => ResourceManager.GetString("EditorChanged", resourceCulture) ?? string.Empty;

	public static string EditorChangedSelected => ResourceManager.GetString("EditorChangedSelected", resourceCulture) ?? string.Empty;

	public static string EditorDepth0 => ResourceManager.GetString("EditorDepth0", resourceCulture) ?? string.Empty;

	public static string EditorDepth1 => ResourceManager.GetString("EditorDepth1", resourceCulture) ?? string.Empty;

	public static string EditorDepth2 => ResourceManager.GetString("EditorDepth2", resourceCulture) ?? string.Empty;

	public static string EditorDepth3 => ResourceManager.GetString("EditorDepth3", resourceCulture) ?? string.Empty;

	public static string EditorSelected => ResourceManager.GetString("EditorSelected", resourceCulture) ?? string.Empty;

	public static string EditorForeground => ResourceManager.GetString("EditorForeground", resourceCulture) ?? string.Empty;

	public static string EditorCurrentStepMarker => ResourceManager.GetString("EditorCurrentStepMarker", resourceCulture) ?? string.Empty;

	public static string EditorPadding => ResourceManager.GetString("EditorPadding", resourceCulture) ?? string.Empty;

	public static string EditorItemSpacing => ResourceManager.GetString("EditorItemSpacing", resourceCulture) ?? string.Empty;

	public static string EditorText => ResourceManager.GetString("EditorText", resourceCulture) ?? string.Empty;

	public static string EditorTimerLabel => ResourceManager.GetString("EditorTimerLabel", resourceCulture) ?? string.Empty;

	public static string EditorTimerValue => ResourceManager.GetString("EditorTimerValue", resourceCulture) ?? string.Empty;

	public static string EditorBackground => ResourceManager.GetString("EditorBackground", resourceCulture) ?? string.Empty;

	public static string EditorConnected => ResourceManager.GetString("EditorConnected", resourceCulture) ?? string.Empty;

	public static string EditorDisconnected => ResourceManager.GetString("EditorDisconnected", resourceCulture) ?? string.Empty;

	public static string EditorLocalMode => ResourceManager.GetString("EditorLocalMode", resourceCulture) ?? string.Empty;

	public static string EditorConnecting => ResourceManager.GetString("EditorConnecting", resourceCulture) ?? string.Empty;

	public static string EditorMaxHeight => ResourceManager.GetString("EditorMaxHeight", resourceCulture) ?? string.Empty;

	public static string EditorInfoSeverity => ResourceManager.GetString("EditorInfoSeverity", resourceCulture) ?? string.Empty;

	public static string EditorErrorSeverity => ResourceManager.GetString("EditorErrorSeverity", resourceCulture) ?? string.Empty;

	public static string EditorWarningSeverity => ResourceManager.GetString("EditorWarningSeverity", resourceCulture) ?? string.Empty;

	public static string EditorFontFamily => ResourceManager.GetString("EditorFontFamily", resourceCulture) ?? string.Empty;

	public static string EditorPanelBackground => ResourceManager.GetString("EditorPanelBackground", resourceCulture) ?? string.Empty;

	public static string EditorPanelHeaderBackground => ResourceManager.GetString("EditorPanelHeaderBackground", resourceCulture) ?? string.Empty;

	public static string EditorSubtleBorder => ResourceManager.GetString("EditorSubtleBorder", resourceCulture) ?? string.Empty;

	public static string EditorSeparator => ResourceManager.GetString("EditorSeparator", resourceCulture) ?? string.Empty;

	public static string EditorSecondaryForeground => ResourceManager.GetString("EditorSecondaryForeground", resourceCulture) ?? string.Empty;

	public static string EditorRestartTitle => ResourceManager.GetString("EditorRestartTitle", resourceCulture) ?? string.Empty;

	public static string EditorRestartMessage => ResourceManager.GetString("EditorRestartMessage", resourceCulture) ?? string.Empty;

	public static string EditorExitNow => ResourceManager.GetString("EditorExitNow", resourceCulture) ?? string.Empty;

	public static string EditorRestartLater => ResourceManager.GetString("EditorRestartLater", resourceCulture) ?? string.Empty;

	public static string EditorCannotSave => ResourceManager.GetString("EditorCannotSave", resourceCulture) ?? string.Empty;

	public static string EditorDefaultFont => ResourceManager.GetString("EditorDefaultFont", resourceCulture) ?? string.Empty;

	public static string ErrorOwnedByAnotherInstance => ResourceManager.GetString("ErrorOwnedByAnotherInstance", resourceCulture) ?? string.Empty;

	public static string ErrorFormulaComputationFailed => ResourceManager.GetString("ErrorFormulaComputationFailed", resourceCulture) ?? string.Empty;

	public static string AtStepFormat => ResourceManager.GetString("AtStepFormat", resourceCulture) ?? string.Empty;

	public static string AtColumnFormat => ResourceManager.GetString("AtColumnFormat", resourceCulture) ?? string.Empty;

	public static string AtRowFormat => ResourceManager.GetString("AtRowFormat", resourceCulture) ?? string.Empty;

	public static string ErrorActionColumnNotFound => ResourceManager.GetString("ErrorActionColumnNotFound", resourceCulture) ?? string.Empty;

	public static string ErrorActionColumnEmpty => ResourceManager.GetString("ErrorActionColumnEmpty", resourceCulture) ?? string.Empty;

	public static string ErrorActionValueNotInteger => ResourceManager.GetString("ErrorActionValueNotInteger", resourceCulture) ?? string.Empty;

	public static string ErrorCsvBodyEmpty => ResourceManager.GetString("ErrorCsvBodyEmpty", resourceCulture) ?? string.Empty;

	public static string ErrorCsvHeaderMismatch => ResourceManager.GetString("ErrorCsvHeaderMismatch", resourceCulture) ?? string.Empty;

	public static string ErrorRecipeFileNotFound => ResourceManager.GetString("ErrorRecipeFileNotFound", resourceCulture) ?? string.Empty;

	public static string ErrorRecipeLoadFailed => ResourceManager.GetString("ErrorRecipeLoadFailed", resourceCulture) ?? string.Empty;

	public static string ErrorRecipeSaveFailed => ResourceManager.GetString("ErrorRecipeSaveFailed", resourceCulture) ?? string.Empty;

	public static string ErrorPropertyValueTypeMismatch => ResourceManager.GetString("ErrorPropertyValueTypeMismatch", resourceCulture) ?? string.Empty;

	public static string ErrorUnsupportedPropertySystemType => ResourceManager.GetString("ErrorUnsupportedPropertySystemType", resourceCulture) ?? string.Empty;

	public static string ErrorGroupValueNotInteger => ResourceManager.GetString("ErrorGroupValueNotInteger", resourceCulture) ?? string.Empty;

	public static string ErrorValueBelowMinimum => ResourceManager.GetString("ErrorValueBelowMinimum", resourceCulture) ?? string.Empty;

	public static string ErrorValueAboveMaximum => ResourceManager.GetString("ErrorValueAboveMaximum", resourceCulture) ?? string.Empty;

	public static string ErrorStringContainsNul => ResourceManager.GetString("ErrorStringContainsNul", resourceCulture) ?? string.Empty;

	public static string ErrorStringTooLong => ResourceManager.GetString("ErrorStringTooLong", resourceCulture) ?? string.Empty;

	public static string ErrorActionByIdNotFound => ResourceManager.GetString("ErrorActionByIdNotFound", resourceCulture) ?? string.Empty;

	public static string ErrorActionByNameNotFound => ResourceManager.GetString("ErrorActionByNameNotFound", resourceCulture) ?? string.Empty;

	public static string ErrorPropertyNotFound => ResourceManager.GetString("ErrorPropertyNotFound", resourceCulture) ?? string.Empty;

	public static string ErrorColumnNotFound => ResourceManager.GetString("ErrorColumnNotFound", resourceCulture) ?? string.Empty;

	public static string ErrorGroupNotFound => ResourceManager.GetString("ErrorGroupNotFound", resourceCulture) ?? string.Empty;

	public static string ErrorValueNotInGroup => ResourceManager.GetString("ErrorValueNotInGroup", resourceCulture) ?? string.Empty;

	public static string ErrorNoStateToUndo => ResourceManager.GetString("ErrorNoStateToUndo", resourceCulture) ?? string.Empty;

	public static string ErrorNoStateToRedo => ResourceManager.GetString("ErrorNoStateToRedo", resourceCulture) ?? string.Empty;

	public static string ErrorInsertIndexOutOfRange => ResourceManager.GetString("ErrorInsertIndexOutOfRange", resourceCulture) ?? string.Empty;

	public static string ErrorStepIndexOutOfRange => ResourceManager.GetString("ErrorStepIndexOutOfRange", resourceCulture) ?? string.Empty;

	public static string ErrorPropertyValueParse => ResourceManager.GetString("ErrorPropertyValueParse", resourceCulture) ?? string.Empty;

	public static string ErrorMaxLoopNestingDepthExceeded => ResourceManager.GetString("ErrorMaxLoopNestingDepthExceeded", resourceCulture) ?? string.Empty;

	public static string ErrorIterationCountUnsupportedType => ResourceManager.GetString("ErrorIterationCountUnsupportedType", resourceCulture) ?? string.Empty;

	public static string WarningUnmatchedEndFor => ResourceManager.GetString("WarningUnmatchedEndFor", resourceCulture) ?? string.Empty;

	public static string WarningUnclosedForLoop => ResourceManager.GetString("WarningUnclosedForLoop", resourceCulture) ?? string.Empty;

	public static string WarningRowCountMismatch => ResourceManager.GetString("WarningRowCountMismatch", resourceCulture) ?? string.Empty;

	public static string CopyStepFailed => ResourceManager.GetString("CopyStepFailed", resourceCulture) ?? string.Empty;

	public static string CutStepFailed => ResourceManager.GetString("CutStepFailed", resourceCulture) ?? string.Empty;

	public static string PasteStepFailed => ResourceManager.GetString("PasteStepFailed", resourceCulture) ?? string.Empty;

	public static string SaveFailed => ResourceManager.GetString("SaveFailed", resourceCulture) ?? string.Empty;

	public static string SaveAsFailed => ResourceManager.GetString("SaveAsFailed", resourceCulture) ?? string.Empty;

	public static string LoadFailed => ResourceManager.GetString("LoadFailed", resourceCulture) ?? string.Empty;

	public static string SyncToggleFailed => ResourceManager.GetString("SyncToggleFailed", resourceCulture) ?? string.Empty;

	public static string StyleEditorFailed => ResourceManager.GetString("StyleEditorFailed", resourceCulture) ?? string.Empty;

	public static string ExitFailed => ResourceManager.GetString("ExitFailed", resourceCulture) ?? string.Empty;

	public static string OrientationToggleFailed => ResourceManager.GetString("OrientationToggleFailed", resourceCulture) ?? string.Empty;

	public static string PlcStateUpdateFailed => ResourceManager.GetString("PlcStateUpdateFailed", resourceCulture) ?? string.Empty;

	public static string PlcConflictHandlingFailed => ResourceManager.GetString("PlcConflictHandlingFailed", resourceCulture) ?? string.Empty;

	public static string SyncTimeRefreshFailed => ResourceManager.GetString("SyncTimeRefreshFailed", resourceCulture) ?? string.Empty;

	public static string PlcConflictResolutionFailed => ResourceManager.GetString("PlcConflictResolutionFailed", resourceCulture) ?? string.Empty;

	public static string AddStepFailed => ResourceManager.GetString("AddStepFailed", resourceCulture) ?? string.Empty;

	public static string DeleteStepFailed => ResourceManager.GetString("DeleteStepFailed", resourceCulture) ?? string.Empty;

	public static string UndoFailed => ResourceManager.GetString("UndoFailed", resourceCulture) ?? string.Empty;

	public static string RedoFailed => ResourceManager.GetString("RedoFailed", resourceCulture) ?? string.Empty;

	public static string PlcConflictDialogShowFailed => ResourceManager.GetString("PlcConflictDialogShowFailed", resourceCulture) ?? string.Empty;

	public static string SaveRecipeFailed => ResourceManager.GetString("SaveRecipeFailed", resourceCulture) ?? string.Empty;

	public static string SavedFormat => ResourceManager.GetString("SavedFormat", resourceCulture) ?? string.Empty;

	public static string LoadedFormat => ResourceManager.GetString("LoadedFormat", resourceCulture) ?? string.Empty;

	public static string PlcReconnect => ResourceManager.GetString("PlcReconnect", resourceCulture) ?? string.Empty;

	public static string StepFormat => ResourceManager.GetString("StepFormat", resourceCulture) ?? string.Empty;

	public static string StepActionChangeFailedFormat => ResourceManager.GetString("StepActionChangeFailedFormat", resourceCulture) ?? string.Empty;

	public static string UnexpectedErrorMessage => ResourceManager.GetString("UnexpectedErrorMessage", resourceCulture) ?? string.Empty;

	public static string OpenRecipeDialogTitle => ResourceManager.GetString("OpenRecipeDialogTitle", resourceCulture) ?? string.Empty;

	public static string SaveRecipeDialogTitle => ResourceManager.GetString("SaveRecipeDialogTitle", resourceCulture) ?? string.Empty;

	public static string RecipeFilesFilter => ResourceManager.GetString("RecipeFilesFilter", resourceCulture) ?? string.Empty;

	public static string AllFilesFilter => ResourceManager.GetString("AllFilesFilter", resourceCulture) ?? string.Empty;

	public static string CsvFilesFilter => ResourceManager.GetString("CsvFilesFilter", resourceCulture) ?? string.Empty;
}
