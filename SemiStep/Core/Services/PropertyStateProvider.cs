using Core.Entities;

namespace Core.Services;

public sealed class PropertyStateProvider
{
	private readonly IReadOnlyList<ColumnDefinition> _columnsInConfig;

	public PropertyStateProvider(IReadOnlyList<ColumnDefinition> columnsInConfig)
	{
		_columnsInConfig = columnsInConfig ?? throw new ArgumentNullException(nameof(columnsInConfig));
	}

	public PropertyState GetPropertyState(Step step, ColumnIdentifier columnKey)
	{
		if (IsStepStartTimeColumn(columnKey))
			return PropertyState.Readonly;

		if (!PropertyExistsInStep(step, columnKey))
			return PropertyState.Disabled;

		var columnDefinition = FindColumnDefinition(columnKey);
		if (columnDefinition == null)
			return PropertyState.Disabled;

		return columnDefinition.ReadOnly
			? PropertyState.Readonly
			: PropertyState.Enabled;
	}

	private static bool IsStepStartTimeColumn(ColumnIdentifier columnKey) =>
		columnKey == MandatoryColumns.StepStartTime;

	private static bool PropertyExistsInStep(Step step, ColumnIdentifier columnKey) =>
		step.Properties.TryGetValue(columnKey, out var propertyValue) && propertyValue != null;

	private ColumnDefinition? FindColumnDefinition(ColumnIdentifier columnKey) =>
		_columnsInConfig.FirstOrDefault(c => c.Key == columnKey);
}
