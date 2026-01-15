using System.Collections.Immutable;

using Core.Definitions.Contracts;
using Core.Domain;
using Core.Entities;
using Core.Properties;

using FluentResults;

namespace Core.Services;

internal sealed class RecipeImporter
{
	private readonly IActionCatalog _actions;
	private readonly IPropertySchema _schema;

	internal RecipeImporter(IActionCatalog actions, IPropertySchema schema)
	{
		_actions = actions ?? throw new ArgumentNullException(nameof(actions));
		_schema = schema ?? throw new ArgumentNullException(nameof(schema));
	}

	internal Result<Recipe> Import(RecipeDto dto)
	{
		if (dto is null)
			return Result.Fail("RecipeDto is null.");

		var stepsBuilder = ImmutableList.CreateBuilder<Step>();

		for (var i = 0; i < dto.Steps.Count; i++)
		{
			var stepResult = ImportStep(dto.Steps[i], i);
			if (stepResult.IsFailed)
				return stepResult.ToResult<Recipe>();

			stepsBuilder.Add(stepResult.Value);
		}

		return Result.Ok(new Recipe(stepsBuilder.ToImmutable()));
	}

	private Result<Step> ImportStep(StepDto dto, int stepIndex)
	{
		if (dto is null)
			return Result.Fail($"StepDto is null at index {stepIndex}.");

		var actionResult = _actions.GetByKey(dto.ActionKey);
		if (actionResult.IsFailed)
			return actionResult.ToResult<Step>();

		var propsBuilder = ImmutableDictionary.CreateBuilder<ColumnId, Property?>();

		foreach (var (columnId, primitive) in dto.Values)
		{
			var schemaResult = _schema.GetDefinition(actionResult.Value, columnId);
			if (schemaResult.IsFailed)
				return schemaResult.ToResult<Step>();

			if (primitive is null)
			{
				propsBuilder[new ColumnId(columnId)] = null;
				continue;
			}

			var propResult = Property.Create(primitive.Value, schemaResult.Value);
			if (propResult.IsFailed)
				return propResult.ToResult<Step>();

			propsBuilder[new ColumnId(columnId)] = propResult.Value;
		}

		return Result.Ok(new Step(propsBuilder.ToImmutable(), dto.DeployDuration.ToEntity()));
	}
}
