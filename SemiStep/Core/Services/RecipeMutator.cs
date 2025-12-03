using Core.Entities;
using Core.Properties;
using Core.Reasons.Errors;

namespace Core.Services;

/// <summary>
/// Handles all Recipe mutations: creating, updating, and removing Steps.
/// </summary>
public sealed class RecipeMutator
{
	private readonly IActionRepository _actionRepository;
	private readonly IActionTargetProvider _actionTargetProvider;
	private readonly PropertyDefinitionRegistry _propertyRegistry;
	private readonly IReadOnlyList<ColumnDefinition> _tableColumns;
	private readonly ILogger<RecipeMutator> _logger;

	public RecipeMutator(
		IActionRepository actionRepository,
		IActionTargetProvider actionTargetProvider,
		PropertyDefinitionRegistry propertyRegistry,
		IReadOnlyList<ColumnDefinition> tableColumns,
		ILogger<RecipeMutator> logger)
	{
		_actionRepository = actionRepository ?? throw new ArgumentNullException(nameof(actionRepository));
		_actionTargetProvider = actionTargetProvider ?? throw new ArgumentNullException(nameof(actionTargetProvider));
		_propertyRegistry = propertyRegistry ?? throw new ArgumentNullException(nameof(propertyRegistry));
		_tableColumns = tableColumns ?? throw new ArgumentNullException(nameof(tableColumns));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Result<Recipe> AddDefaultStep(Recipe recipe, int rowIndex)
	{
		var actionResult = _actionRepository.GetResultDefaultActionId();
		if (actionResult.IsFailed)
			return actionResult.ToResult();

		var stepResult = CreateDefaultStep(actionResult.Value);
		if (stepResult.IsFailed)
			return stepResult.ToResult();

		var clampedIndex = Math.Max(0, Math.Min(rowIndex, recipe.Steps.Count));
		return Result.Ok(new Recipe(recipe.Steps.Insert(clampedIndex, stepResult.Value)));
	}

	public Result<Recipe> RemoveStep(Recipe recipe, int rowIndex)
	{
		return rowIndex < 0 || rowIndex >= recipe.Steps.Count
			? new CoreIndexOutOfRangeError(rowIndex, recipe.Steps.Count)
			: new Recipe(recipe.Steps.RemoveAt(rowIndex));
	}

	public Result<Recipe> UpdateStepProperty(Recipe recipe, int rowIndex, ColumnIdentifier key, object value)
	{
		if (rowIndex < 0 || rowIndex >= recipe.Steps.Count)
			return new CoreIndexOutOfRangeError(rowIndex, recipe.Steps.Count);

		var step = recipe.Steps[rowIndex];

		if (!step.Properties.TryGetValue(key, out var property) || property == null)
			return new CoreStepPropertyNotFoundError(key.Value, rowIndex);

		var newPropertyResult = property.WithValue(value);
		if (newPropertyResult.IsFailed)
			return newPropertyResult.ToResult().WithError(new CoreStepPropertyUpdateFailedError(rowIndex, key.Value));

		var updatedProperties = step.Properties.SetItem(key, newPropertyResult.Value);
		var updatedStep = step with { Properties = updatedProperties };
		return new Recipe(recipe.Steps.SetItem(rowIndex, updatedStep));
	}

	public Result<Recipe> ReplaceStepAction(Recipe recipe, int rowIndex, short newActionId)
	{
		if (rowIndex < 0 || rowIndex >= recipe.Steps.Count)
			return new CoreIndexOutOfRangeError(rowIndex, recipe.Steps.Count);

		var stepResult = CreateDefaultStep(newActionId);
		if (stepResult.IsFailed)
		{
			_logger.LogError(
				"Failed to create default step for action ID {ActionId} when replacing step at index {RowIndex}",
				newActionId,
				rowIndex);
			return stepResult.ToResult();
		}

		return new Recipe(recipe.Steps.SetItem(rowIndex, stepResult.Value));
	}


	private Result<Step> CreateDefaultStep(short actionId)
	{
		var actionResult = _actionRepository.GetActionDefinitionById(actionId);
		if (actionResult.IsFailed)
			return actionResult.ToResult();

		var builderResult = StepBuilder.Create(actionResult.Value, _propertyRegistry, _tableColumns);
		if (builderResult.IsFailed)
			return builderResult.ToResult();
		var builder = builderResult.Value;

		foreach (var col in actionResult.Value.Columns.Where(c =>
					 c.PropertyTypeId.Equals("Enum", StringComparison.OrdinalIgnoreCase) &&
					 !string.IsNullOrWhiteSpace(c.GroupName)))
		{
			var key = new ColumnIdentifier(col.Key);
			if (!builder.Supports(key))
				continue;

			var minimalTargetResult = _actionTargetProvider.GetMinimalTargetId(col.GroupName!);
			if (minimalTargetResult.IsFailed)
				return Result.Fail(new CoreStepFailedToSetDefaultTarget(col.Key))
					.WithErrors(minimalTargetResult.Errors);

			var targetId = minimalTargetResult.Value;
			var setResult = builder.WithOptionalDynamic(key, targetId);
			if (setResult.IsFailed)
			{
				_logger.LogError(new InvalidOperationException(setResult.Errors.First().Message),
					"Failed to set default target for column '{ColumnKey}'",
					col.Key);
				return new CoreStepFailedToSetDefaultTarget(col.Key);
			}
		}

		return Result.Ok(builder.Build());
	}

	public Result<Recipe> InsertSteps(Recipe recipe, int index, IReadOnlyList<Step> steps)
	{
		if (recipe == null)
			throw new ArgumentNullException(nameof(recipe));
		if (steps == null)
			throw new ArgumentNullException(nameof(steps));
		if (steps.Count == 0)
			return recipe;

		var clampedIndex = Math.Max(0, Math.Min(index, recipe.Steps.Count));
		var updatedSteps = recipe.Steps;

		foreach (var step in steps)
		{
			updatedSteps = updatedSteps.Insert(clampedIndex, step);
			clampedIndex++;
		}

		return new Recipe(updatedSteps);
	}

	public Result<Recipe> RemoveSteps(Recipe recipe, IReadOnlyCollection<int> indices)
	{
		if (recipe == null)
			throw new ArgumentNullException(nameof(recipe));
		if (indices == null)
			throw new ArgumentNullException(nameof(indices));
		if (indices.Count == 0)
			return recipe;

		var sortedIndices = indices
			.Where(i => i >= 0 && i < recipe.Steps.Count)
			.OrderByDescending(i => i)
			.ToList();

		if (sortedIndices.Count == 0)
			return recipe;

		var updatedSteps = recipe.Steps;

		foreach (var index in sortedIndices)
		{
			updatedSteps = updatedSteps.RemoveAt(index);
		}

		return new Recipe(updatedSteps);
	}
}
