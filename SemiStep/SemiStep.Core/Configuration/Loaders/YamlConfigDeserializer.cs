using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemiStep.Core.Configuration.Loaders;

internal static class YamlConfigDeserializer
{
	public static IDeserializer Instance { get; } = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.WithDuplicateKeyChecking()
		.Build();
}
