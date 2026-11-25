using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// The change marks each recipe state had when the surface left it, keyed by the Recipe instance.
/// </summary>
internal sealed class ChangeMarkHistory(ILogger logger)
{
	// Reference keys: two equal recipes are separate history points; a recipe that left history takes its marks along.
	private readonly ConditionalWeakTable<Recipe, IReadOnlySet<string>[]> _marksByRecipe = [];

	public void Capture(Recipe recipe, IReadOnlyList<RecipeRowViewModel> rows)
	{
		if (!rows.Any(row => row.ChangedColumns.Count > 0))
		{
			_marksByRecipe.Remove(recipe);
			return;
		}

		var rowMarks = new IReadOnlySet<string>[rows.Count];
		for (var i = 0; i < rows.Count; i++)
		{
			rowMarks[i] = rows[i].ChangedColumns;
		}

		_marksByRecipe.AddOrUpdate(recipe, rowMarks);
	}

	public void Restore(Recipe recipe, IReadOnlyList<RecipeRowViewModel> rows)
	{
		if (!_marksByRecipe.TryGetValue(recipe, out var rowMarks))
		{
			return;
		}

		if (rowMarks.Length != rows.Count)
		{
			logger.LogWarning(
				"Change marks not restored: {StoredRowCount} stored rows, {RowCount} shown rows",
				rowMarks.Length,
				rows.Count);
			return;
		}

		for (var i = 0; i < rows.Count; i++)
		{
			if (rowMarks[i].Count > 0)
			{
				rows[i].MarkChanged(rowMarks[i]);
			}
		}
	}
}
