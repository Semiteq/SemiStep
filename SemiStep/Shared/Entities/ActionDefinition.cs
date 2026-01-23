namespace Shared.Entities;

public sealed record ActionDefinition(
	short Id,
	string UiName,
	string DeployDuration,
	IReadOnlyList<ActionColumnDefinition> Columns);
