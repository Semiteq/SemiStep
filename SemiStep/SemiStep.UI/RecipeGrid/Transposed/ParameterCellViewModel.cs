using System.ComponentModel;

using ReactiveUI;

namespace SemiStep.UI.RecipeGrid.Transposed;

public abstract class ParameterCellViewModel : ReactiveObject, IDisposable
{
	private const string RowIndexerName = "Item";

	protected ParameterCellViewModel(RecipeRowViewModel recipeRowViewModel, ParameterDescriptor parameterDescriptor)
	{
		Row = recipeRowViewModel;
		Descriptor = parameterDescriptor;
		Row.PropertyChanged += OnRowPropertyChanged;
	}

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

	private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case RowIndexerName:
				this.RaisePropertyChanged(nameof(Value));
				break;
			case nameof(RecipeRowViewModel.StepStartTime)
				when IsStepStartTimeParameter():
				this.RaisePropertyChanged(nameof(Value));
				break;
			case nameof(RecipeRowViewModel.InapplicableColumns):
				this.RaisePropertyChanged(nameof(IsApplicable));
				break;
			case nameof(RecipeRowViewModel.ChangedColumns):
				this.RaisePropertyChanged(nameof(IsChanged));
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
