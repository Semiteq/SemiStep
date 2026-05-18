using System.Globalization;

using FluentResults;

using Microsoft.Extensions.Logging;

using NCalc;

using SemiStep.Core.Recipes.Formulas.Errors;

namespace SemiStep.Core.Recipes.Formulas;

public sealed class FormulaEvaluator
{
	private readonly ILogger<FormulaEvaluator> _logger;
	private readonly RecipeMetadataRegistry _registry;

	public FormulaEvaluator(RecipeMetadataRegistry registry, ILogger<FormulaEvaluator> logger)
	{
		_registry = registry;
		_logger = logger;
	}

	public Result<Step> Recalculate(
		Step step,
		ActionDefinition action,
		string changedColumnKey)
	{
		if (action.Formula is null)
		{
			throw new InvalidOperationException(
				$"FormulaEvaluator.Recalculate called for action '{action.UiName}' (id={action.Id}) without a Formula.");
		}

		var formula = action.Formula;
		var target = SelectTarget(formula, changedColumnKey);

		if (!formula.CompiledExpressions.TryGetValue(target, out var compiled))
		{
			throw new InvalidOperationException(
				$"No compiled expression registered for target '{target}' in action '{action.UiName}'.");
		}

		var variableValues = BuildVariableMap(step, formula, action);

		var evaluationResult = EvaluateExpression(compiled, variableValues, target, action.Id);
		if (evaluationResult.IsFailed)
		{
			return evaluationResult.ToResult<Step>();
		}

		var computed = evaluationResult.Value;

		var propertyDefinition = ResolveTargetPropertyDefinition(action, target);

		var convertResult = ConvertToPropertyValue(target, computed, propertyDefinition);
		if (convertResult.IsFailed)
		{
			return convertResult.ToResult<Step>();
		}

		var newPropertyValue = convertResult.Value;

		var validationResult = ValidateNewValue(action, target, newPropertyValue, propertyDefinition);
		if (validationResult.IsFailed)
		{
			return validationResult.ToResult<Step>();
		}

		return Result.Ok(step.WithProperty(target, newPropertyValue));
	}

	private Result<double> EvaluateExpression(
		NCalc.Domain.LogicalExpression compiled,
		Dictionary<string, object> variableValues,
		string target,
		int actionId)
	{
		double computed;
		try
		{
			var expression = new Expression(compiled, ExpressionOptions.None, CultureInfo.InvariantCulture);
			foreach (var (name, value) in variableValues)
			{
				expression.Parameters[name] = value;
			}

			var raw = expression.Evaluate();
			if (raw is null)
			{
				return Result.Fail<double>(new FormulaComputationFailedError(
					target,
					"Expression evaluated to null."));
			}

			computed = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
		}
		catch (Exception evaluationException)
		{
			_logger.LogInformation(
				evaluationException,
				"Formula evaluation failed for action {ActionId} target {Target}",
				actionId,
				target);
			return Result.Fail<double>(new FormulaComputationFailedError(target, evaluationException.Message));
		}

		if (double.IsNaN(computed) || double.IsInfinity(computed))
		{
			var token = double.IsNaN(computed) ? "NaN" : "Infinity";
			return Result.Fail<double>(new FormulaComputationFailedError(
				target,
				$"Expression produced non-finite value '{token}' ({computed.ToString(CultureInfo.InvariantCulture)})."));
		}

		return Result.Ok(computed);
	}

	private PropertyTypeDefinition ResolveTargetPropertyDefinition(ActionDefinition action, string target)
	{
		var targetActionPropertyResult = action.FindProperty(target);
		if (targetActionPropertyResult.IsFailed)
		{
			throw new InvalidOperationException(
				$"Target '{target}' is in recalc_order but not in action '{action.UiName}'.");
		}

		var propertyDefinitionResult = _registry.GetProperty(targetActionPropertyResult.Value.PropertyTypeId);
		if (propertyDefinitionResult.IsFailed)
		{
			throw new InvalidOperationException(
				$"Property type '{targetActionPropertyResult.Value.PropertyTypeId}' for target '{target}' not found in registry.");
		}

		return propertyDefinitionResult.Value;
	}

	private Result ValidateNewValue(
		ActionDefinition action,
		string target,
		PropertyValue newPropertyValue,
		PropertyTypeDefinition propertyDefinition)
	{
		var rangeCheckValue = Convert.ToDouble(newPropertyValue.Value, CultureInfo.InvariantCulture);

		var validation = PropertyValidator.Validate(propertyDefinition, newPropertyValue.Value);
		if (validation.IsFailed)
		{
			var min = propertyDefinition.Min;
			var max = propertyDefinition.Max;
			var isOutOfRange =
				(min.HasValue && rangeCheckValue < min.Value)
				|| (max.HasValue && rangeCheckValue > max.Value);

			if (isOutOfRange)
			{
				return Result.Fail(new FormulaTargetOutOfRangeError(
					target,
					rangeCheckValue,
					min,
					max).CausedBy(validation.Errors));
			}

			return Result.Fail(new FormulaComputationFailedError(
				target,
				string.Join("; ", validation.Errors.Select(e => e.Message))).CausedBy(validation.Errors));
		}

		var targetActionProperty = action.FindProperty(target).Value;
		var groupCheck = PropertyValidator.ValidateGroupValue(targetActionProperty, newPropertyValue, _registry);
		if (groupCheck.IsFailed)
		{
			return Result.Fail(new FormulaComputationFailedError(
				target,
				string.Join("; ", groupCheck.Errors.Select(e => e.Message))).CausedBy(groupCheck.Errors));
		}

		return Result.Ok();
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

			if (propertyValue.Value is not (int or float))
			{
				throw new InvalidOperationException(
					$"Recalc-order variable '{variableKey}' has non-numeric value of type {propertyValue.Type}.");
			}

			map[variableKey] = Convert.ToDouble(propertyValue.Value, CultureInfo.InvariantCulture);
		}

		return map;
	}

	private static string SelectTarget(FormulaDefinition formula, string changedColumnKey)
	{
		// Mapper enforces recalc_order has >= 2 distinct entries, so a target distinct from
		// changedColumnKey is guaranteed to exist.
		return formula.RecalcOrder.First(variable =>
			!string.Equals(variable, changedColumnKey, StringComparison.OrdinalIgnoreCase));
	}

	private static Result<PropertyValue> ConvertToPropertyValue(
		string target,
		double computed,
		PropertyTypeDefinition propertyDefinition)
	{
		if (SystemTypes.Comparer.Equals(propertyDefinition.SystemType, SystemTypes.Int))
		{
			var rounded = Math.Round(computed, MidpointRounding.ToEven);
			if (rounded > int.MaxValue || rounded < int.MinValue)
			{
				return Result.Fail<PropertyValue>(
					new FormulaComputationFailedError(
						target,
						$"Recalculated value {computed.ToString(CultureInfo.InvariantCulture)} overflows Int32."));
			}

			return Result.Ok(PropertyValue.FromInt((int)rounded));
		}

		if (SystemTypes.Comparer.Equals(propertyDefinition.SystemType, SystemTypes.Float))
		{
			var floatValue = (float)computed;
			if (!float.IsFinite(floatValue))
			{
				return Result.Fail<PropertyValue>(
					new FormulaComputationFailedError(
						target,
						$"Recalculated value {computed.ToString(CultureInfo.InvariantCulture)} is not representable as a finite float."));
			}

			return Result.Ok(PropertyValue.FromFloat(floatValue));
		}

		throw new InvalidOperationException(
			$"Unsupported target system type '{propertyDefinition.SystemType}'. "
			+ "Mapper rejects non-int/non-float targets at config-load; this branch is unreachable.");
	}
}
