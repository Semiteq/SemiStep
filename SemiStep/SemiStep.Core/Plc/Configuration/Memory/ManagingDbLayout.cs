namespace SemiStep.Core.Plc.Configuration.Memory;

public sealed record ManagingDbLayout(
	int DbNumber,
	int VersionOffset,
	int CommittedOffset,
	int RecipeLinesOffset,
	int TotalSize)
{
	public static ManagingDbLayout Default => new(
		DbNumber: 2,
		VersionOffset: 0,
		CommittedOffset: 4,
		RecipeLinesOffset: 6,
		TotalSize: 10);
}
