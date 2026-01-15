using Core.Definitions;
using Core.Properties.Contracts;

namespace Core.ConfigImport.Runtime;

internal sealed record BuiltCoreConfig(
	IReadOnlyDictionary<string, ActionDefinition> ActionsByKey,
	IReadOnlyDictionary<(string ActionKey, string ColumnId), IPropertyDefinition> PropertySchema);
