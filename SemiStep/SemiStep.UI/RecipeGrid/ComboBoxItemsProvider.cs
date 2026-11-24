using System.Collections.Frozen;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class ComboBoxItemsProvider(RecipeMetadataRegistry recipeMetadataRegistry)
{
	private readonly FrozenDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> _itemsByGroup =
		BuildItemsByGroup(recipeMetadataRegistry);

	public IReadOnlyList<ComboBoxItemViewModel> ActionItems { get; } = [.. recipeMetadataRegistry.GetAllActions()
		.Select(action => new ComboBoxItemViewModel(action.Id, action.UiName))];

	/// <summary>Returns an empty list for a group no action references.</summary>
	public IReadOnlyList<ComboBoxItemViewModel> GetGroupItems(string groupName)
	{
		return _itemsByGroup.TryGetValue(groupName, out var items) ? items : [];
	}

	private static FrozenDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> BuildItemsByGroup(
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		return recipeMetadataRegistry.GetAllActions()
			.SelectMany(action => action.Properties)
			.Select(property => property.GroupName)
			.OfType<string>()
			.Where(groupName => !string.IsNullOrEmpty(groupName))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToFrozenDictionary(
				groupName => groupName,
				groupName => BuildGroupItems(recipeMetadataRegistry, groupName),
				StringComparer.OrdinalIgnoreCase);
	}

	private static IReadOnlyList<ComboBoxItemViewModel> BuildGroupItems(
		RecipeMetadataRegistry recipeMetadataRegistry,
		string groupName)
	{
		return [.. recipeMetadataRegistry.GetGroup(groupName).Value.Items
			.OrderBy(entry => entry.Key)
			.Select(entry => new ComboBoxItemViewModel(entry.Key, entry.Value))];
	}
}
