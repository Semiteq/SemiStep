namespace TypesShared.Plc;

public sealed record PlcConnectionSettings(
	string IpAddress,
	int Port,
	int Rack,
	int Slot)
{
	public static PlcConnectionSettings Default => new(
		IpAddress: "192.168.0.150",
		Port: 102,
		Rack: 0,
		Slot: 2);
}
