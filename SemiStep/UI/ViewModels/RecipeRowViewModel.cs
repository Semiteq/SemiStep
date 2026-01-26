using ReactiveUI;

using Recipe.Entities;

using Shared.Entities;
using Shared.Registries;

namespace UI.ViewModels;

public class RecipeRowViewModel(
	int stepNumber,
	Step step,
	ActionDefinition action,
	IPropertyRegistry propertyRegistry,
	Action<int, string, object?> onPropertyChanged)
	: ReactiveObject
{
	private readonly IPropertyRegistry _propertyRegistry = propertyRegistry;
	private bool _isExecuting;

	public int StepNumber { get; } = stepNumber;

	public string ActionName => action.UiName;

	public bool IsExecuting
	{
		get => _isExecuting;
		set => this.RaiseAndSetIfChanged(ref _isExecuting, value);
	}

	public object? GetPropertyValue(string propertyTypeId)
	{
		var columnId = new ColumnId(propertyTypeId);
		if (step.Properties.TryGetValue(columnId, out var propertyValue))
		{
			return propertyValue.Value;
		}

		return null;
	}

	public void SetPropertyValue(string propertyTypeId, object? value)
	{
		onPropertyChanged(StepNumber - 1, propertyTypeId, value);
		this.RaisePropertyChanged(propertyTypeId);
	}
}
