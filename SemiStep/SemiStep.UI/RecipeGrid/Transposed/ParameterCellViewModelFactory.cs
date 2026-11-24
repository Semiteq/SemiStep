using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class ParameterCellViewModelFactory
{
	private readonly Func<IReadOnlyList<ComboBoxItemViewModel>> _actionItemsProvider;

	private readonly IReadOnlyDictionary<string, int?> _maxLengthByColumn;

	public ParameterCellViewModelFactory(
		RecipeMetadataRegistry recipeMetadataRegistry,
		ComboBoxItemsProvider comboBoxItemsProvider)
	{
		var actionItems = comboBoxItemsProvider.ActionItems;
		_actionItemsProvider = () => actionItems;
		_maxLengthByColumn = StringColumnMaxLengths.Build(recipeMetadataRegistry);
	}

	public ParameterCellViewModel Create(RecipeRowViewModel recipeRowViewModel, ParameterDescriptor parameterDescriptor)
	{
		if (ColumnTypes.IsActionComboBoxColumn(parameterDescriptor.ColumnType))
		{
			return new ComboBoxCellViewModel(recipeRowViewModel, parameterDescriptor, _actionItemsProvider);
		}

		return ColumnTypes.IsGroupBoundColumn(parameterDescriptor.ColumnType)
			? new ComboBoxCellViewModel(
				recipeRowViewModel,
				parameterDescriptor,
				() => recipeRowViewModel.GroupItemsByColumn[parameterDescriptor.ParameterKey])
			: parameterDescriptor.IsReadOnlyParameter
			|| ColumnTypes.IsStepStartTimeColumn(parameterDescriptor.ColumnType)
			? new ReadOnlyCellViewModel(recipeRowViewModel, parameterDescriptor)
			: new PropertyTextCellViewModel(
			recipeRowViewModel,
			parameterDescriptor,
			_maxLengthByColumn.GetValueOrDefault(parameterDescriptor.ParameterKey));
	}
}
