using TypesShared.Plc.Memory;

namespace TypesShared.Plc;

public sealed record PlcProtocolLayout(
	ManagingDbLayout ManagingDb,
	DataDbLayout IntDb,
	DataDbLayout FloatDb,
	DataDbLayout StringDb,
	ExecutionDbLayout ExecutionDb)
{
	public static PlcProtocolLayout Default => new(
		ManagingDb: ManagingDbLayout.Default,
		IntDb: DataDbLayout.DefaultInt,
		FloatDb: DataDbLayout.DefaultFloat,
		StringDb: DataDbLayout.DefaultString,
		ExecutionDb: ExecutionDbLayout.Default);
}
