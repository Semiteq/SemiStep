namespace SemiStep.Tools.PlcEmulator;

/// <summary>What the emulator serves; capacities count ints, floats and strings of the data DBs.</summary>
internal sealed record EmulatorOptions(
	string ConfigDir,
	int Port,
	int IntCapacity,
	int FloatCapacity,
	int StringCapacity);
