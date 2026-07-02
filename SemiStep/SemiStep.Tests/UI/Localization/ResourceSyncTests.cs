using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using FluentAssertions;

using SemiStep.UI.Localization;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class ResourceSyncTests
{
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

	public static IEnumerable<object[]> FormatKeys()
	{
		var keyNames = new[]
		{
			nameof(Resources.LastSyncAgoFormat),
			nameof(Resources.LastSyncPrefix),
			nameof(Resources.MessagePanelErrorsFormat),
			nameof(Resources.MessagePanelWarningsFormat),
			nameof(Resources.PlcConflictLocalSteps),
			nameof(Resources.PlcConflictPlcSteps)
		};

		return keyNames.Select(keyName => new object[] { keyName });
	}

	[Theory]
	[MemberData(nameof(FormatKeys))]
	public void FormatKey_ContainsPlaceholder_UnderEnglishAndRussian(string keyName)
	{
		var property = typeof(Resources).GetProperty(keyName, BindingFlags.Public | BindingFlags.Static)!;

		foreach (var culture in new[] { "en", "ru" })
		{
			using (ResourcesCultureScope.Use(culture))
			{
				var value = (string)property.GetValue(null)!;

				value.Should().Contain(
					"{0}", "format key '{0}' must keep its placeholder under culture '{1}'", keyName, culture);
			}
		}
	}
}
