using Domain.State;

using Recipe.Analysis;
using Recipe.Entities;
using Recipe.Exceptions;
using Recipe.Services;
using Recipe.Validation;

using Shared.Entities;
using Shared.Registries;

namespace Domain.Services;

public sealed class RecipeService(
	RecipeStateManager state,
	RecipeMutator recipeMutator,
	PropertyValidator propertyValidator,
	RecipeAnalyzer recipeAnalyzer,
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry,
	IColumnRegistry columnRegistry)
{
	private readonly PropertyValidator _propertyValidator = propertyValidator;

	public Recipe.Entities.Recipe CurrentRecipe => state.Current;
	public bool IsDirty => state.IsDirty;
	public bool IsValid => state.IsValid;
	public AnalysisResult? LastAnalysis => state.LastAnalysis;

	public void AddStep(short actionId)
	{
		var action = actionRegistry.GetAction(actionId);
		var properties = ResolvePropertiesForAction(action);
		var newRecipe = recipeMutator.AddStep(state.Current, action, properties);
		var analysis = recipeAnalyzer.Analyze(newRecipe);
		state.Update(newRecipe, analysis);
	}

	public void InsertStep(int index, short actionId)
	{
		var action = actionRegistry.GetAction(actionId);
		var properties = ResolvePropertiesForAction(action);
		var newRecipe = recipeMutator.InsertStep(state.Current, index, action, properties);
		var analysis = recipeAnalyzer.Analyze(newRecipe);
		state.Update(newRecipe, analysis);
	}

	public void ChangeStepAction(int stepIndex, short newActionId)
	{
		var newAction = actionRegistry.GetAction(newActionId);
		var properties = ResolvePropertiesForAction(newAction);
		var newRecipe = recipeMutator.ChangeStepAction(state.Current, stepIndex, newAction, properties);
		var analysis = recipeAnalyzer.Analyze(newRecipe);
		state.Update(newRecipe, analysis);
	}

	public void RemoveStep(int index)
	{
		var newRecipe = recipeMutator.RemoveStep(state.Current, index);
		var analysis = recipeAnalyzer.Analyze(newRecipe);
		state.Update(newRecipe, analysis);
	}

	public void UpdateProperty(int stepIndex, string columnKey, object value)
	{
		var columnDef = columnRegistry.GetColumn(columnKey);
		var property = propertyRegistry.GetProperty(columnDef.PropertyTypeId);
		var validation = PropertyValidator.Validate(property, value);
		if (!validation.IsValid)
		{
			throw new PropertyValidationException(validation);
		}

		var columnId = new ColumnId(columnKey);
		var propertyValue = CreatePropertyValue(value);
		var newRecipe = recipeMutator.UpdateProperty(state.Current, stepIndex, columnId, propertyValue);
		var analysis = recipeAnalyzer.Analyze(newRecipe);
		state.Update(newRecipe, analysis);
	}

	public ValidationResult ValidateProperty(string columnKey, object value)
	{
		var columnDef = columnRegistry.GetColumn(columnKey);
		var property = propertyRegistry.GetProperty(columnDef.PropertyTypeId);
		return PropertyValidator.Validate(property, value);
	}

	public void LoadRecipe(Recipe.Entities.Recipe recipe)
	{
		var analysis = recipeAnalyzer.Analyze(recipe);
		state.Load(recipe, analysis);
	}

	public void NewRecipe()
	{
		state.Reset();
	}

	public void MarkSaved()
	{
		state.MarkSaved();
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
			float => PropertyType.Float,
			double => PropertyType.Float,
			string => PropertyType.String,
			_ => throw new ArgumentException($"Unsupported value type: {value.GetType()}")
		};
		return new PropertyValue(value, type);
	}
}
