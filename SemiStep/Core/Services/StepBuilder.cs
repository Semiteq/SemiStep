using System.Collections.Immutable;

using Core.Entities;
using Core.Properties;
using Core.Reasons.Errors;

namespace Core.Services;

/// <summary>
/// Stateful builder to construct a Step for a specific action using dynamic PropertyTypeId.
/// </summary>
public sealed class StepBuilder
{
	private readonly ActionDefinition _actionDefinition;
	private ImmutableDictionary<ColumnIdentifier, Property?> _properties;

	private StepBuilder(
		ActionDefinition actionDefinition,
		ImmutableDictionary<ColumnIdentifier, Property?> properties)
	{
		_actionDefinition = actionDefinition;
		_properties = properties;
	}

	/// <summary>
	/// Creates and initializes a new StepBuilder instance.
	/// </summary>
	public static Result<StepBuilder> Create(
		ActionDefinition actionDefinition,
		PropertyDefinitionRegistry propertyRegistry,
		IReadOnlyList<ColumnDefinition> tableColumns)
	{
		var propertiesResult = InitializeProperties(actionDefinition, propertyRegistry, tableColumns);
		if (propertiesResult.IsFailed)
			return propertiesResult.ToResult();

		return new StepBuilder(actionDefinition, propertiesResult.Value);
	}

	/// <summary>
	/// Constructs and returns the final immutable Step object.
	/// </summary>
	public Step Build() => new(_properties, _actionDefinition.DeployDuration);

	/// <summary>
	/// Checks whether a specific column is supported (applicable) for the current action.
	/// </summary>
	public bool Supports(ColumnIdentifier key)
	{
		if (key == MandatoryColumns.Action)
			return true;
		return _actionDefinition.Columns.Any(c => c.Key.Equals(key.Value, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Sets a property with a new value if the action supports the property.
	/// </summary>
	public Result<StepBuilder> WithOptionalDynamic(ColumnIdentifier key, object value)
	{
		if (!Supports(key))
			return this;

		if (!_properties.TryGetValue(key, out var existingProperty) || existingProperty == null)
			return this;

		var propertyResult = existingProperty.WithValue(value);
		if (propertyResult.IsFailed)
			return propertyResult.WithError(new CorePropertyDefaultValueFailedError(key.Value)).ToResult();

		_properties = _properties.SetItem(key, propertyResult.Value);
		return this;
	}

	private static Result<ImmutableDictionary<ColumnIdentifier, Property?>> InitializeProperties(
		ActionDefinition actionDefinition,
		PropertyDefinitionRegistry propertyRegistry,
		IReadOnlyList<ColumnDefinition> tableColumns)
	{
		var properties = CreateEmptyProperties(tableColumns);

		var actionResult = AddActionProperty(properties, actionDefinition, propertyRegistry);
		if (actionResult.IsFailed)
			return actionResult;

		var columnsResult = AddColumnProperties(actionResult.Value, actionDefinition, propertyRegistry);
		if (columnsResult.IsFailed)
			return columnsResult;

		return columnsResult.Value;
	}

	private static ImmutableDictionary<ColumnIdentifier, Property?> CreateEmptyProperties(
		IReadOnlyList<ColumnDefinition> tableColumns)
	{
		var builder = ImmutableDictionary.CreateBuilder<ColumnIdentifier, Property?>();
		foreach (var column in tableColumns)
			builder[column.Key] = null;
		return builder.ToImmutable();
	}

	private static Result<ImmutableDictionary<ColumnIdentifier, Property?>> AddActionProperty(
		ImmutableDictionary<ColumnIdentifier, Property?> properties,
		ActionDefinition actionDefinition,
		PropertyDefinitionRegistry propertyRegistry)
	{
		var propertyDefinition = propertyRegistry.GetPropertyDefinition("Enum");
		var propertyResult = Property.Create(actionDefinition.Id, propertyDefinition);
		return propertyResult.IsFailed
			? propertyResult.WithError(new CoreActionPropertyCreationFailedError(actionDefinition.Id)).ToResult()
			: properties.SetItem(MandatoryColumns.Action, propertyResult.Value);
	}

	private static Result<ImmutableDictionary<ColumnIdentifier, Property?>> AddColumnProperties(
		ImmutableDictionary<ColumnIdentifier, Property?> properties,
		ActionDefinition actionDefinition,
		PropertyDefinitionRegistry propertyRegistry)
	{
		var current = properties;

		foreach (var column in actionDefinition.Columns)
		{
			var result = AddColumnProperty(current, column, propertyRegistry);
			if (result.IsFailed)
				return result;
			current = result.Value;
		}

		return current;
	}

	private static Result<ImmutableDictionary<ColumnIdentifier, Property?>> AddColumnProperty(
		ImmutableDictionary<ColumnIdentifier, Property?> properties,
		PropertyConfig column,
		PropertyDefinitionRegistry propertyRegistry)
	{
		var propertyDefinition = propertyRegistry.GetPropertyDefinition(column.PropertyTypeId);

		var defaultValue = propertyDefinition.DefaultValue;
		if (column.DefaultValue != null)
		{
			var parseResult = propertyDefinition.TryParse(column.DefaultValue);
			if (parseResult.IsFailed)
				return parseResult.WithError(new CorePropertyParsingFailedError(column.DefaultValue, column.Key))
					.ToResult();

			defaultValue = parseResult.Value;
		}

		var propertyResult = Property.Create(defaultValue, propertyDefinition);
		if (propertyResult.IsFailed)
			return propertyResult.WithError(new CorePropertyCreationFailedError(column.Key)).ToResult();


		var key = new ColumnIdentifier(column.Key);
		return properties.SetItem(key, propertyResult.Value);
	}
}
