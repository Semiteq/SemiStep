using System.ComponentModel;

namespace SemiStep.UI.RecipeGrid.Transposed;

public abstract class ParameterCellViewModel : INotifyPropertyChanged, IDisposable
{
	private const string RowIndexerName = "Item";

	protected ParameterCellViewModel(RecipeRowViewModel recipeRowViewModel, ParameterDescriptor parameterDescriptor)
	{
		Row = recipeRowViewModel;
		Descriptor = parameterDescriptor;
		Row.PropertyChanged += OnRowPropertyChanged;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public RecipeRowViewModel Row { get; }

	public ParameterDescriptor Descriptor { get; }

	public object? Value
	{
		get => Row[Descriptor.ParameterKey];
		set => WriteValue(value);
	}

	public bool IsApplicable => Row.IsApplicable(Descriptor.ParameterKey);

	public bool IsChanged => Row.IsChanged(Descriptor.ParameterKey);

	public string FormatKind => Row.ColumnFormatKinds.GetValueOrDefault(
		Descriptor.ParameterKey,
		TimeFormatHelper.DefaultFormatKind);

	public string? Units => Row.ColumnUnits.GetValueOrDefault(Descriptor.ParameterKey);

	public virtual void Dispose()
	{
		Row.PropertyChanged -= OnRowPropertyChanged;
	}

	protected virtual void WriteValue(object? value)
	{
		Row[Descriptor.ParameterKey] = value;
	}

	protected void RaisePropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case RowIndexerName:
				RaisePropertyChanged(nameof(Value));
				break;
			case nameof(RecipeRowViewModel.StepStartTime)
				when IsStepStartTimeParameter():
				RaisePropertyChanged(nameof(Value));
				break;
			case nameof(RecipeRowViewModel.InapplicableColumns):
				RaisePropertyChanged(nameof(IsApplicable));
				break;
			case nameof(RecipeRowViewModel.ChangedColumns):
				RaisePropertyChanged(nameof(IsChanged));
				break;
		}
	}

	private bool IsStepStartTimeParameter()
	{
		return string.Equals(
			Descriptor.ParameterKey,
			TimeFormatHelper.StepStartTimeColumnKey,
			StringComparison.OrdinalIgnoreCase);
	}
}
