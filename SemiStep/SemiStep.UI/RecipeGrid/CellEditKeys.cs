using Avalonia.Input;

namespace SemiStep.UI.RecipeGrid;

/// <summary>The keys that start a cell edit in either grid.</summary>
internal static class CellEditKeys
{
	/// <summary>F2: starts the edit of any editable cell.</summary>
	public static bool IsEditKey(KeyEventArgs e)
	{
		return e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.None;
	}

	/// <summary>F4 or Alt+Down: opens the list of a combo cell.</summary>
	public static bool IsListKey(KeyEventArgs e)
	{
		return (e.Key == Key.F4 && e.KeyModifiers == KeyModifiers.None)
			|| (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt);
	}

	/// <summary>A character that types into a text cell: no command modifier and no control character.</summary>
	public static bool IsPrintable(string? text, KeyModifiers modifiers)
	{
		return !string.IsNullOrEmpty(text)
			&& (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0
			&& !char.IsControl(text[0]);
	}
}
