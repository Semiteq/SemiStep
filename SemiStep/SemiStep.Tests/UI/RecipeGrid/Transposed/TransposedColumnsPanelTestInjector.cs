using Avalonia.Controls;
using Avalonia.Controls.Templates;

using SemiStep.UI.RecipeGrid.Transposed;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Swaps the <c>StepListBox</c> items panel to <see cref="TransposedColumnsPanel"/> so tests exercise
/// the recycle-in-place panel against the real view, mirroring the production template line-swap while
/// binding <see cref="TransposedColumnsPanel.ColumnWidth"/> to the same width resource.
/// </summary>
internal static class TransposedColumnsPanelTestInjector
{
	public static void UseTransposedColumnsPanel(this ListBox stepListBox)
	{
		stepListBox.ItemsPanel = new FuncTemplate<Panel?>(() =>
		{
			var panel = new TransposedColumnsPanel();
			panel.Bind(
				TransposedColumnsPanel.ColumnWidthProperty,
				stepListBox.GetResourceObservable(TransposedRecipeGridView.StepColumnWidthResourceKey));

			return panel;
		});
	}
}
