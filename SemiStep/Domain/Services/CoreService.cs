using Core.Analysis;
using Core.Entities;
using Core.Services;

using Domain.State;

using Shared.Entities;
using Shared.Registries;

namespace Domain.Services;

public sealed class CoreService(
	CoreFacade coreFacade,
	RecipeStateManager stateManager,
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry,
	IColumnRegistry columnRegistry)
{
	public Recipe CurrentRecipe => stateManager.Current;
	public RecipeSnapshot? LastSnapshot => stateManager.LastSnapshot;
	public bool IsDirty => stateManager.IsDirty;
	public bool IsValid => stateManager.IsValid;

	public RecipeSnapshot NewRecipe()
	{
		var result = coreFacade.Analyze(Recipe.Empty);
		stateManager.Reset();
		stateManager.Update(result);
		return result;
	}

	public RecipeSnapshot AppendStep(int actionId)
	{
		var action = actionRegistry.GetAction(actionId);
		var properties = ResolvePropertiesForAction(action);
		var result = coreFacade.AppendStep(stateManager.Current, action, properties);
		ApplyResult(result);
		return result;
	}

	public RecipeSnapshot InsertStep(int index, int actionId)
	{
		var action = actionRegistry.GetAction(actionId);
		var properties = ResolvePropertiesForAction(action);
		var result = coreFacade.InsertStep(stateManager.Current, index, action, properties);
		ApplyResult(result);
		return result;
	}

	public RecipeSnapshot ChangeStepAction(int stepIndex, int newActionId)
	{
		var newAction = actionRegistry.GetAction(newActionId);
		var properties = ResolvePropertiesForAction(newAction);
		var result = coreFacade.ChangeStepAction(stateManager.Current, stepIndex, newAction, properties);
		ApplyResult(result);
		return result;
	}

	public RecipeSnapshot RemoveStep(int index)
	{
		var result = coreFacade.RemoveStep(stateManager.Current, index);
		ApplyResult(result);
		return result;
	}

	public RecipeSnapshot UpdateProperty(int stepIndex, string columnKey, object value)
	{
		var columnDef = columnRegistry.GetColumn(columnKey);
		var property = propertyRegistry.GetProperty(columnDef.PropertyTypeId);

		var step = stateManager.Current.Steps[stepIndex];
		var action = actionRegistry.GetAction(step.ActionKey);

		var columnId = new ColumnId(columnKey);
		var propertyValue = CreatePropertyValue(value);

		// TODO: Support formula definitions when formula registry is implemented
		var result = coreFacade.UpdateProperty(
			stateManager.Current,
			stepIndex,
			columnId,
			propertyValue,
			property,
			action,
			formulaDefinition: null);

		ApplyResult(result);
		return result;
	}

	public RecipeSnapshot LoadRecipe(Recipe recipe)
	{
		var result = coreFacade.Analyze(recipe);
		stateManager.Load(result);
		return result;
	}

	public void MarkSaved()
	{
		stateManager.MarkSaved();
	}

	private void ApplyResult(RecipeSnapshot snapshot)
	{
		stateManager.Update(snapshot);
	}

	private IReadOnlyList<PropertyDefinition> ResolvePropertiesForAction(ActionDefinition action)
	{
		return action.Columns
			.Select(col => propertyRegistry.GetProperty(col.PropertyTypeId))
			.ToList();
	}

	private static PropertyValue CreatePropertyValue(object value)
	{
		var type = value switch
		{
			int => PropertyType.Int,
			float or double => PropertyType.Float,
			string => PropertyType.String,
			_ => throw new ArgumentException($"Unsupported value type: {value.GetType()}")
		};
		return new PropertyValue(value, type);
	}
}
