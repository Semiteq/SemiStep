using FluentResults;

namespace Core.Properties;

internal static class PropertyError
{
	internal static Error UnsupportedType(string type) => new($"Type {type} is unsupported");
	internal static Error TypeMismatch(string expectedType, string actualType) => new($"Expected {expectedType}, got {actualType}");
	internal static Error ConversionFailed(string from, string to) => new($"Conversion failed for {from} to {to}");
}
