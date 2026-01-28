namespace Shared.Entities;

public sealed record ActionDefinition(
	int Id,
	string UiName,
	string DeployDuration,
	IReadOnlyList<ActionColumnDefinition> Columns);
