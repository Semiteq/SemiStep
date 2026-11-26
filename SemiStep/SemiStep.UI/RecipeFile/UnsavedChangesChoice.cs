namespace SemiStep.UI.RecipeFile;

// Cancel must stay first: dismissing the dialog (title-bar X, Alt+F4) makes
// ShowDialog<UnsavedChangesChoice> return default, which must mean Cancel, not Save.
public enum UnsavedChangesChoice
{
	Cancel,
	Save,
	DontSave
}
