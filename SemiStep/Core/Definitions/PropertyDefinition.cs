using Core.Properties.Contracts;

namespace Core.Definitions;

internal sealed record PropertyDefinition(string ColumnId, IPropertyDefinition Definition);
