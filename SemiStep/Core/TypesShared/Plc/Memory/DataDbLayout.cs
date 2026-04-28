namespace TypesShared.Plc.Memory;

public sealed record DataDbLayout(
	int DbNumber,
	int CapacityOffset,
	int CurrentSizeOffset,
	int DataStartOffset)
{
	public static DataDbLayout DefaultInt => new(
		DbNumber: 3,
		CapacityOffset: 0,
		CurrentSizeOffset: 4,
		DataStartOffset: 8);

	public static DataDbLayout DefaultFloat => new(
		DbNumber: 4,
		CapacityOffset: 0,
		CurrentSizeOffset: 4,
		DataStartOffset: 8);

	public static DataDbLayout DefaultString => new(
		DbNumber: 5,
		CapacityOffset: 0,
		CurrentSizeOffset: 4,
		DataStartOffset: 8);
}
