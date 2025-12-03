namespace Core.Entities;

public sealed record PortableStepDto
{
	public short ActionId { get; }
	public IReadOnlyDictionary<string, string> RawValues { get; }

	public PortableStepDto(short actionId, IReadOnlyDictionary<string, string> rawValues)
	{
		ActionId = actionId;
		RawValues = rawValues ?? throw new ArgumentNullException(nameof(rawValues));
	}
}
