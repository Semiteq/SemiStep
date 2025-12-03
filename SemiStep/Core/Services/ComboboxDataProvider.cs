using Core.Reasons.Errors;

namespace Core.Services;

/// <inheritdoc />
public sealed class ComboboxDataProvider : IComboboxDataProvider
{
	private readonly IActionRepository _actions;
	private readonly IActionTargetProvider _targets;

	public ComboboxDataProvider(
		IActionRepository actionRepository,
		IActionTargetProvider actionTargetProvider)
	{
		_actions = actionRepository ?? throw new ArgumentNullException(nameof(actionRepository));
		_targets = actionTargetProvider ?? throw new ArgumentNullException(nameof(actionTargetProvider));
	}

	/// <inheritdoc />
	public Result<IReadOnlyDictionary<short, string>> GetResultEnumOptions(short actionId, string columnKey)
	{
		var actionResult = _actions.GetActionDefinitionById(actionId);
		if (actionResult.IsFailed)
			return actionResult.ToResult();

		var columnResult = GetColumn(actionResult.Value, columnKey);
		if (columnResult.IsFailed)
			return columnResult.ToResult();

		var validationResult = ValidateColumn(columnResult.Value);
		if (validationResult.IsFailed)
			return validationResult.ToResult();

		var groupName = columnResult.Value.GroupName;
		return _targets.GetFilteredGroupTargets(groupName);
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<short, string> GetActions()
		=> _actions.Actions.Values.ToDictionary(a => a.Id, a => a.Name);

	private static Result<PropertyConfig> GetColumn(ActionDefinition action, string columnKey)
	{
		var column = action.Columns.FirstOrDefault(c =>
			string.Equals(c.Key, columnKey, StringComparison.OrdinalIgnoreCase));

		return column == null
			? new CoreColumnNotFoundInActionError(action.Name, action.Id, columnKey)
			: column;
	}

	private static Result<PropertyConfig> ValidateColumn(PropertyConfig column)
	{
		return string.IsNullOrWhiteSpace(column.GroupName)
			? new CoreColumnGroupNameEmptyError()
			: Result.Ok(column);
	}
}
