using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class ParameterCellViewModelFactory
{
	private readonly Func<IReadOnlyList<ComboBoxItemViewModel>> _actionItemsProvider;

	private readonly IReadOnlyDictionary<string, int?> _maxLengthByColumn;

	public ParameterCellViewModelFactory(RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var actionItems = recipeMetadataRegistry.GetActionComboBoxItems();
		_actionItemsProvider = () => actionItems;
		_maxLengthByColumn = StringColumnMaxLengths.Build(recipeMetadataRegistry);
	}

	public ParameterCellViewModel Create(RecipeRowViewModel recipeRowViewModel, ParameterDescriptor parameterDescriptor)
	{
		if (ColumnTypes.IsActionComboBoxColumn(parameterDescriptor.ColumnType))
		{
			return new ComboBoxCellViewModel(recipeRowViewModel, parameterDescriptor, _actionItemsProvider);
		}

		if (ColumnTypes.IsGroupBoundColumn(parameterDescriptor.ColumnType))
		{
			return new ComboBoxCellViewModel(
				recipeRowViewModel,
				parameterDescriptor,
				() => recipeRowViewModel.GroupItemsByColumn[parameterDescriptor.ParameterKey]);
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
}
