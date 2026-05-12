namespace SemiStep.Core.Plc.Configuration;

public sealed record PlcConfiguration(
	PlcConnectionSettings Connection,
	PlcProtocolSettings ProtocolSettings,
	PlcProtocolLayout Layout)
{
	public static PlcConfiguration Default => new(
		PlcConnectionSettings.Default,
		PlcProtocolSettings.Default,
		PlcProtocolLayout.Default);
}
