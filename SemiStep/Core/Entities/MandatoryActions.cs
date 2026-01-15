namespace Core.Entities;

/// <summary>
/// Service actions with locked IDs not present in config YAML.
/// </summary>
internal enum MandatoryActions
{
	Wait = 10,
	ForLoop = 20,
	EndForLoop = 30,
	Pause = 40
}
