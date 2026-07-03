namespace SemiStep.UI.ShutdownService;

// Cancel must stay first: dismissing the dialog (title-bar X, Alt+F4) makes
// ShowDialog<ExitConfirmationResult> return default, which must mean Cancel, not Save.
public enum ExitConfirmationResult
{
	Cancel,
	Save,
	DontSave
}
