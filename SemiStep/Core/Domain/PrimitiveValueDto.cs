using OneOf;

namespace Core.Domain;

public sealed record PrimitiveValueDto(OneOf<int, float, string> Value)
{
	public static PrimitiveValueDto FromInt(int value) => new(value);
	public static PrimitiveValueDto FromFloat(float value) => new(value);
	public static PrimitiveValueDto FromString(string value) => new(value ?? string.Empty);
}
