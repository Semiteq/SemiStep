using FluentAssertions;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Configuration.Mapping;

[Trait("Category", "Unit")]
[Trait("Component", "Config")]
[Trait("Area", "Mapping")]
public sealed class ActionMapperRoleTargetsTests
{
	[Fact]
	public void TryMap_NullRole_DefaultsToAction()
	{
		var dto = BuildBaseActionDto();
		dto.Role = null;

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsSuccess.Should().BeTrue();
		result.Value.Role.Should().Be(ActionRole.Action);
	}

	[Fact]
	public void TryMap_ExplicitActionRole_MapsToAction()
	{
		var dto = BuildBaseActionDto();
		dto.Role = "action";

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsSuccess.Should().BeTrue();
		result.Value.Role.Should().Be(ActionRole.Action);
	}

	[Fact]
	public void TryMap_SubactionRole_MapsToSubaction()
	{
		var dto = BuildBaseActionDto();
		dto.Role = "subaction";

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsSuccess.Should().BeTrue();
		result.Value.Role.Should().Be(ActionRole.Subaction);
	}

	[Fact]
	public void TryMap_InvalidRoleString_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Role = "primary";

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Unsupported role 'primary'"));
	}

	[Fact]
	public void TryMap_SelectorColumnTargets_SurviveMapping()
	{
		var dto = BuildBaseActionDto();
		dto.Columns!.Add(new ActionColumnDto
		{
			Key = "icp_match",
			GroupName = "match_mode",
			PropertyTypeId = "temp",
			Targets = new Dictionary<int, int> { [2] = 3002 }
		});

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsSuccess.Should().BeTrue();
		var selectorColumn = result.Value.Properties.Single(p => p.Key == "icp_match");
		selectorColumn.Targets.Should().NotBeNull();
		selectorColumn.Targets.Should().ContainKey(2).WhoseValue.Should().Be(3002);
	}

	[Fact]
	public void TryMap_NonSelectorColumns_HaveNoTargets()
	{
		var dto = BuildBaseActionDto();

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsSuccess.Should().BeTrue();
		result.Value.Properties.Should().OnlyContain(p => p.Targets == null);
	}

	private static IReadOnlyDictionary<string, PropertyTypeDefinition> BuildDefaultProperties()
	{
		return new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["temp"] = new PropertyTypeDefinition("temp", "float", "decimal", null, null, null, null),
			["speed"] = new PropertyTypeDefinition("speed", "float", "decimal", null, null, null, null),
			["duration"] = new PropertyTypeDefinition("duration", "float", "decimal", null, null, null, null)
		};
	}

	private static ActionDto BuildBaseActionDto()
	{
		return new ActionDto
		{
			Id = 300,
			UiName = "process",
			DeployDuration = "longlasting",
			Columns = new List<ActionColumnDto>
			{
				new() { Key = "task", PropertyTypeId = "temp" },
				new() { Key = "speed", PropertyTypeId = "speed" },
				new() { Key = "step_duration", PropertyTypeId = "duration" }
			}
		};
	}
}
