using FluentResults;

using Microsoft.Extensions.Logging;

using NCalc;

using SemiStep.Core.Recipes.Formulas.Errors;

namespace SemiStep.Core.Recipes.Formulas;

public sealed class FormulaEvaluator
{
	private readonly ILogger<FormulaEvaluator> _logger;

	public FormulaEvaluator(ILogger<FormulaEvaluator> logger)
	{
		_logger = logger;
	}

	public Result<Step> Recalculate(
		Step step,
		ActionDefinition action,
		string changedColumnKey,
		RecipeMetadataRegistry registry)
	{
		if (action.Formula is null)
		{
			throw new InvalidOperationException(
				$"FormulaEvaluator.Recalculate called for action '{action.UiName}' (id={action.Id}) without a Formula.");
		}

		var formula = action.Formula;

		var variableValues = BuildVariableMap(step, formula, action);

		var target = SelectTarget(formula, changedColumnKey);
		if (target is null)
		{
			throw new InvalidOperationException(
				$"Recalc order for action '{action.UiName}' contains no target distinct from '{changedColumnKey}'.");
		}

		if (!formula.CompiledExpressions.TryGetValue(target, out var compiled))
		{
			throw new InvalidOperationException(
				$"No compiled expression registered for target '{target}' in action '{action.UiName}'.");
		}

		double computed;
		try
		{
			var expression = new Expression(compiled);
			foreach (var (name, value) in variableValues)
			{
				expression.Parameters[name] = value;
			}

			var raw = expression.Evaluate();
			if (raw is null)
			{
				return Result.Fail(new FormulaComputationFailedError(
					target,
					"Expression evaluated to null."));
			}

			computed = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
		}
		catch (Exception evaluationException)
		{
			_logger.LogInformation(
				evaluationException,
				"Formula evaluation failed for action {ActionId} target {Target}",
				action.Id,
				target);
			return Result.Fail(new FormulaComputationFailedError(target, evaluationException.Message));
		}

		if (double.IsNaN(computed) || double.IsInfinity(computed))
		{
			return Result.Fail(new FormulaComputationFailedError(
				target,
				$"Expression produced non-finite value '{computed}'."));
		}

		var targetActionPropertyResult = action.FindProperty(target);
		if (targetActionPropertyResult.IsFailed)
		{
			throw new InvalidOperationException(
				$"Target '{target}' is in recalc_order but not in action '{action.UiName}'.");
		}

		var targetActionProperty = targetActionPropertyResult.Value;

		var propertyDefinitionResult = registry.GetProperty(targetActionProperty.PropertyTypeId);
		if (propertyDefinitionResult.IsFailed)
		{
			throw new InvalidOperationException(
				$"Property type '{targetActionProperty.PropertyTypeId}' for target '{target}' not found in registry.");
		}

		var propertyDefinition = propertyDefinitionResult.Value;

		var convertResult = ConvertToPropertyValue(target, computed, propertyDefinition);
		if (convertResult.IsFailed)
		{
			return convertResult.ToResult<Step>();
		}

		var newPropertyValue = convertResult.Value;

		var validation = PropertyValidator.Validate(propertyDefinition, newPropertyValue.Value);
		if (validation.IsFailed)
		{
			return Result.Fail(new FormulaTargetOutOfRangeError(
				target,
				computed,
				propertyDefinition.Min,
				propertyDefinition.Max));
		}

		return Result.Ok(step.WithProperty(target, newPropertyValue));
	}

	private static Dictionary<string, object> BuildVariableMap(
		Step step,
		FormulaDefinition formula,
		ActionDefinition action)
	{
		var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		foreach (var variableKey in formula.RecalcOrder)
		{
			if (!step.Properties.TryGetValue(new PropertyId(variableKey), out var propertyValue))
			{
				throw new InvalidOperationException(
					$"Recalc-order variable '{variableKey}' is missing from step for action '{action.UiName}'.");
			}

			map[variableKey] = propertyValue.Value switch
			{
				int intValue => intValue,
				float floatValue => floatValue,
				double doubleValue => doubleValue,
				_ => throw new InvalidOperationException(
					$"Recalc-order variable '{variableKey}' has non-numeric value of type {propertyValue.Type}.")
			};
		}

		return map;
	}

	private static string? SelectTarget(FormulaDefinition formula, string changedColumnKey)
	{
		foreach (var variable in formula.RecalcOrder)
		{
			if (!string.Equals(variable, changedColumnKey, StringComparison.OrdinalIgnoreCase))
			{
				return variable;
			}
		}

		return null;
	}

	private static Result<PropertyValue> ConvertToPropertyValue(
		string target,
		double computed,
		PropertyTypeDefinition propertyDefinition)
	{
		return propertyDefinition.SystemType.ToLowerInvariant() switch
		{
			"int" => ConvertToInt(target, computed),
			"float" => Result.Ok(PropertyValue.FromFloat((float)computed)),
			_ => Result.Fail<PropertyValue>(
				new FormulaComputationFailedError(
					target,
					$"Unsupported target system type '{propertyDefinition.SystemType}'."))
		};
	}

	private static Result<PropertyValue> ConvertToInt(string target, double computed)
	{
		var rounded = Math.Round(computed, MidpointRounding.ToEven);
		if (rounded > int.MaxValue || rounded < int.MinValue)
		{
			return Result.Fail<PropertyValue>(
				new FormulaComputationFailedError(
					target,
					$"Recalculated value {computed} overflows Int32."));
		}

		return Result.Ok(PropertyValue.FromInt((int)rounded));
	}
}
