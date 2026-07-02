using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using FluentAssertions;

using SemiStep.UI.Localization;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class ResourceSyncTests
{
	// tryParents: false reads only the requested culture's own entries, so a key missing
	// from a satellite is absent here instead of silently falling back to the neutral value.
	private static readonly Dictionary<string, string> _neutral = ReadOwnResourceSet(CultureInfo.InvariantCulture);
	private static readonly Dictionary<string, string> _russian = ReadOwnResourceSet(new CultureInfo("ru"));

	public static IEnumerable<object[]> LocalizedKeys()
	{
		return typeof(Resources)
			.GetProperties(BindingFlags.Public | BindingFlags.Static)
			.Where(property => property.PropertyType == typeof(string))
			.Select(property => new object[] { property.Name });
	}

	[Theory]
	[MemberData(nameof(LocalizedKeys))]
	public void Key_ResolvesToNonEmptyValue_UnderEnglishAndRussian(string keyName)
	{
		var property = typeof(Resources).GetProperty(keyName, BindingFlags.Public | BindingFlags.Static)!;

		foreach (var culture in new[] { "en", "ru" })
		{
			using (ResourcesCultureScope.Use(culture))
			{
				var value = (string)property.GetValue(null)!;

				value.Should().NotBeNullOrEmpty(
					"key '{0}' must resolve under culture '{1}' (missing resx entry?)", keyName, culture);
			}
		}
	}

	[Fact]
	public void RussianSatellite_ContainsEveryNeutralKey_AndNoOrphans()
	{
		// ResourceManager falls back to the neutral value for a missing satellite key, so the
		// per-key resolution test above cannot catch an untranslated key. Compare the raw key
		// sets instead: every English key must have a Russian entry, and no orphan ru keys exist.
		_russian.Keys.Should().BeEquivalentTo(_neutral.Keys,
			"every neutral (English) resx key must have a matching Russian translation, and vice versa");
	}

	[Fact]
	public void EveryValue_IsNonEmpty_InBothSatellites()
	{
		_neutral.Values.Should().NotContain(string.Empty, "no English resx value may be blank");
		_russian.Values.Should().NotContain(string.Empty, "no Russian resx value may be blank");
	}

	[Fact]
	public void PlaceholderSets_Match_BetweenEnglishAndRussian()
	{
		foreach (var (key, englishValue) in _neutral)
		{
			var englishPlaceholders = Placeholders(englishValue);
			var russianPlaceholders = Placeholders(_russian.GetValueOrDefault(key, string.Empty));

			russianPlaceholders.Should().BeEquivalentTo(englishPlaceholders,
				"key '{0}' must use the same {{n}} format placeholders in English and Russian", key);
		}
	}

	private static ISet<int> Placeholders(string value)
	{
		return Regex.Matches(value, @"\{(\d+)")
			.Select(match => int.Parse(match.Groups[1].Value))
			.ToHashSet();
	}

	private static Dictionary<string, string> ReadOwnResourceSet(CultureInfo culture)
	{
		var set = Resources.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!;

		return set.Cast<DictionaryEntry>()
			.ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
	}
}
