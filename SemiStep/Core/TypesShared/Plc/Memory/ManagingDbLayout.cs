namespace TypesShared.Plc.Memory;

public sealed record ManagingDbLayout(
	int DbNumber,
	int CommittedOffset,
	int RecipeLinesOffset,
	int TotalSize)
{
	public static ManagingDbLayout Default => new(
		DbNumber: 2,
		CommittedOffset: 0,
		RecipeLinesOffset: 2,
		TotalSize: 6);
}
