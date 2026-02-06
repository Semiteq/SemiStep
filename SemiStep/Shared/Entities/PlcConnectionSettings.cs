namespace Shared.Entities;

public sealed record PlcConnectionSettings(
	string IpAddress,
	int Rack,
	int Slot,
	int HeaderDbNumber)
{
	public const int DefaultPort = 102;
	public const int DefaultRack = 0;
	public const int DefaultSlot = 1;
	public const int DefaultHeaderDbNumber = 1;

	public static PlcConnectionSettings Default => new(
		IpAddress: "192.168.0.1",
		Rack: DefaultRack,
		Slot: DefaultSlot,
		HeaderDbNumber: DefaultHeaderDbNumber);
}
