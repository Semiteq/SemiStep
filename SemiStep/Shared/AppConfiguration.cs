using Shared.Entities;

namespace Shared;

public sealed record AppConfiguration(
	IReadOnlyDictionary<string, PropertyDefinition> Properties,
	IReadOnlyDictionary<string, GridColumnDefinition> Columns,
	IReadOnlyDictionary<string, GroupDefinition> Groups,
	IReadOnlyDictionary<int, ActionDefinition> Actions,
	GridStyleOptions GridStyle);
