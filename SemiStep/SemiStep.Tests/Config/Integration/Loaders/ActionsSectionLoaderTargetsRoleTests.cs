using FluentAssertions;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loaders;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "NestedActions")]
public sealed class ActionsSectionLoaderTargetsRoleTests
{
	private const string NestedActionsYaml = """
		300:
		  ui_name: "Etching"
		  role: action
		  deploy_duration: "immediate"
		  columns:
		    - key: icp_power
		      property_type_id: power_icp
		      default_value: "0"
		    - key: icp_match
		      group_name: match_mode
		      property_type_id: enum
		      targets: { 2: 3002 }
		3002:
		  ui_name: "ICP manual"
		  role: subaction
		  deploy_duration: "immediate"
		  columns:
		    - key: icp_load
		      property_type_id: percent
		      default_value: "50"
		""";

	private const string BackwardCompatYaml = """
		100:
		  ui_name: "Simple"
		  deploy_duration: "immediate"
		  columns:
		    - key: power
		      property_type_id: power
		      default_value: "0"
		""";

	[Fact]
	public async Task ActionsLoader_DeserializesTargetsAndSubactionRole()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("actions", "process.yaml"), NestedActionsYaml);

		var result = await ActionsSectionLoader.LoadAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(e => e.Message))
			: string.Empty);

		var primary = result.Value.Single(action => action.Id == 300);
		primary.Role.Should().Be("action");

		var selectorColumn = primary.Columns!.Single(column => column.Key == "icp_match");
		selectorColumn.Targets.Should().NotBeNull();
		selectorColumn.Targets!.Should().ContainKey(2).WhoseValue.Should().Be(3002);

		var subaction = result.Value.Single(action => action.Id == 3002);
		subaction.Role.Should().Be("subaction");
	}

	[Fact]
	public async Task ActionsLoader_AbsentTargetsAndRoleDeserializeAsNull()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("actions", "process.yaml"), BackwardCompatYaml);

		var result = await ActionsSectionLoader.LoadAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(e => e.Message))
			: string.Empty);

		var action = result.Value.Single(definition => definition.Id == 100);
		action.Role.Should().BeNull();
		action.Columns!.Single().Targets.Should().BeNull();
	}
}
