using TypesShared.Core;
using TypesShared.Plc;
using TypesShared.Style;

namespace TypesShared.Config;

public sealed record AppConfiguration(
	IReadOnlyDictionary<string, PropertyTypeDefinition> Properties,
	IReadOnlyDictionary<string, GridColumnDefinition> Columns,
	IReadOnlyDictionary<string, GroupDefinition> Groups,
	IReadOnlyDictionary<int, ActionDefinition> Actions,
	GridStyleOptions GridStyle,
	PlcConfiguration PlcConfiguration);
