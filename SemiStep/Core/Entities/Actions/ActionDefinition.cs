using Core.Properties;

namespace Core.Entities.Actions;

internal record ActionDefinition
	(
		short Id,
		string Name,
		IReadOnlyList<Property> Properties,
		DeployDuration DeployDuration
	);
