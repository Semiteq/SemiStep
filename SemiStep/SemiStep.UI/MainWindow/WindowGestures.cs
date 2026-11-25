using System.Globalization;

using Avalonia.Input;

using SemiStep.UI.Localization;

namespace SemiStep.UI.MainWindow;

/// <summary>
/// The window's gestures: each menu item shows one, the window runs the item by it, tooltips name it.
/// </summary>
public static class WindowGestures
{
	public static readonly KeyGesture NewRecipe = new(Key.N, KeyModifiers.Control | KeyModifiers.Shift);

	public static readonly KeyGesture OpenRecipe = new(Key.O, KeyModifiers.Control);

	public static readonly KeyGesture SaveRecipe = new(Key.S, KeyModifiers.Control);

	public static readonly KeyGesture SaveRecipeAs = new(Key.S, KeyModifiers.Control | KeyModifiers.Shift);

	public static readonly KeyGesture Exit = new(Key.F4, KeyModifiers.Alt);

	public static readonly KeyGesture AddStep = new(Key.N, KeyModifiers.Control);

	public static readonly KeyGesture DeleteStep = new(Key.Delete);

	public static readonly KeyGesture CopySteps = new(Key.C, KeyModifiers.Control);

	public static readonly KeyGesture CutSteps = new(Key.X, KeyModifiers.Control);

	public static readonly KeyGesture PasteSteps = new(Key.V, KeyModifiers.Control);

	public static readonly KeyGesture Undo = new(Key.Z, KeyModifiers.Control);

	public static readonly KeyGesture Redo = new(Key.Y, KeyModifiers.Control);

	public static readonly KeyGesture RedoAlternate = new(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);

	public static readonly KeyGesture ToggleOrientation = new(Key.T, KeyModifiers.Control | KeyModifiers.Shift);

	private static readonly KeyGesture[] _stepEditing = [DeleteStep, CopySteps, CutSteps, PasteSteps];

	public static string OpenRecipeTip => TipOf(Resources.TipOpenRecipe, OpenRecipe);

	public static string SaveRecipeTip => TipOf(Resources.TipSaveRecipe, SaveRecipe);

	public static string AddStepTip => TipOf(Resources.ContextAddStep, AddStep);

	public static string DeleteStepTip => TipOf(Resources.ContextDeleteStep, DeleteStep);

	public static string CopyStepsTip => TipOf(Resources.ContextCopyStep, CopySteps);

	public static string CutStepsTip => TipOf(Resources.ContextCutStep, CutSteps);

	public static string PasteStepsTip => TipOf(Resources.ContextPasteStep, PasteSteps);

	public static string UndoTip => TipOf(Resources.ToolbarUndo, Undo);

	public static string RedoTip => TipOf(Resources.ToolbarRedo, Redo);

	/// <summary>True for a gesture that a cell editor owns while a cell is being edited.</summary>
	public static bool BelongsToCellEditor(KeyGesture gesture)
	{
		return Array.IndexOf(_stepEditing, gesture) >= 0;
	}

	private static string TipOf(string label, KeyGesture gesture)
	{
		return string.Format(
			CultureInfo.InvariantCulture,
			Resources.TooltipWithGestureFormat,
			label,
			gesture.ToString("p", null));
	}
}
