using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Configuration;

public sealed record AppConfiguration(
	IReadOnlyDictionary<string, PropertyTypeDefinition> Properties,
	IReadOnlyDictionary<string, GridColumnDefinition> Columns,
	IReadOnlyDictionary<string, GroupDefinition> Groups,
	IReadOnlyDictionary<int, ActionDefinition> Actions,
	GridStyleOptions GridStyle,
	PlcConfiguration PlcConfiguration);
