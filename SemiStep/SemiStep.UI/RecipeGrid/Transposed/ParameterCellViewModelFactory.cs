using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class ParameterCellViewModelFactory(RecipeMetadataRegistry recipeMetadataRegistry)
{
	private readonly IReadOnlyList<ComboBoxItemViewModel> _actionItems =
		recipeMetadataRegistry.GetActionComboBoxItems();

	private readonly IReadOnlyDictionary<string, int?> _maxLengthByColumn =
		BuildMaxLengths(recipeMetadataRegistry);

	public ParameterCellViewModel Create(RecipeRowViewModel recipeRowViewModel, ParameterDescriptor parameterDescriptor)
	{
		if (ColumnTypes.IsActionComboBoxColumn(parameterDescriptor.ColumnType))
		{
			return new ActionComboBoxCellViewModel(recipeRowViewModel, parameterDescriptor, _actionItems);
		}

		if (ColumnTypes.IsGroupBoundColumn(parameterDescriptor.ColumnType))
		{
			return new TargetComboBoxCellViewModel(recipeRowViewModel, parameterDescriptor);
		}

		if (parameterDescriptor.IsReadOnlyParameter
			|| ColumnTypes.IsStepStartTimeColumn(parameterDescriptor.ColumnType))
		{
			return new ReadOnlyCellViewModel(recipeRowViewModel, parameterDescriptor);
		}

		return new PropertyTextCellViewModel(
			recipeRowViewModel,
			parameterDescriptor,
			_maxLengthByColumn.GetValueOrDefault(parameterDescriptor.ParameterKey));
	}

	// Mirror of ColumnBuilder.ResolveMaxLength: string-typed columns cap the editor at the
	// PLC string block length; every other type has no editor length limit.
	private static IReadOnlyDictionary<string, int?> BuildMaxLengths(RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var result = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in recipeMetadataRegistry.GetAllColumns())
		{
			var propertyResult = recipeMetadataRegistry.GetProperty(column.PropertyTypeId);
			var isStringTyped = propertyResult.IsSuccess
				&& SystemTypes.Comparer.Equals(propertyResult.Value.SystemType, SystemTypes.String);

			result[column.Key] = isStringTyped ? recipeMetadataRegistry.GetStringMaxLength() : null;
		}

		return result;
	}
}
