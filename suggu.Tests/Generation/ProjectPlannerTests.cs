using suggu.Core.Generation;
using suggu.Core.Inspection;
using suggu.Core.Planning;

namespace suggu.Tests.Generation;

public sealed class ProjectPlannerTests
{
    private static readonly string Solution = Path.Combine("D:", "Shop", "Shop.slnx");

    [Fact]
    public void Api_project_plans_webapi_template_and_solution_add()
    {
        var plan = ProjectPlanner.BuildPlan(Solution, "Shop.Api", ProjectType.Api, framework: "9");

        var project = Assert.IsType<CreateProjectOperation>(plan.Operations[0]);
        Assert.Equal("webapi", project.Template);
        Assert.Equal("net9.0", project.Framework);
        Assert.Null(project.ExtraArguments);
        Assert.IsType<AddProjectToSolutionOperation>(plan.Operations[1]);
    }

    [Fact]
    public void Controllers_flag_maps_to_use_controllers()
    {
        var plan = ProjectPlanner.BuildPlan(Solution, "Shop.Api", ProjectType.Api, useControllers: true);

        var project = Assert.IsType<CreateProjectOperation>(plan.Operations[0]);
        Assert.Equal(["--use-controllers"], project.ExtraArguments);
    }

    [Fact]
    public void Mvc_uses_mvc_template_and_ignores_controllers_flag()
    {
        var plan = ProjectPlanner.BuildPlan(Solution, "Shop.Web", ProjectType.Mvc, useControllers: true);

        var project = Assert.IsType<CreateProjectOperation>(plan.Operations[0]);
        Assert.Equal("mvc", project.Template);
        Assert.Null(project.ExtraArguments);
    }

    [Fact]
    public void Console_uses_console_template_and_ignores_controllers_flag()
    {
        var plan = ProjectPlanner.BuildPlan(Solution, "Shop.Worker", ProjectType.Console, framework: "10", useControllers: true);

        var project = Assert.IsType<CreateProjectOperation>(plan.Operations[0]);
        Assert.Equal("console", project.Template);
        Assert.Equal("net10.0", project.Framework);
        Assert.Null(project.ExtraArguments);
        Assert.IsType<AddProjectToSolutionOperation>(plan.Operations[1]);
    }

    [Fact]
    public void Without_solution_no_sln_add_is_planned()
    {
        var plan = ProjectPlanner.BuildPlan(null, "Standalone", ProjectType.Api, parentDirectory: Path.Combine("D:", "tmp"));

        Assert.Single(plan.Operations);
        Assert.IsType<CreateProjectOperation>(plan.Operations[0]);
    }

    [Fact]
    public void New_solution_is_created_first_then_project_then_added()
    {
        var dir = Path.Combine("D:", "TestCli");
        var plan = ProjectPlanner.BuildPlan(
            null, "DummyCliApi.Api", ProjectType.Api,
            newSolution: new ProjectPlanner.NewSolution(dir, "DummyCliApi"));

        var solution = Assert.IsType<CreateSolutionOperation>(plan.Operations[0]);
        var project = Assert.IsType<CreateProjectOperation>(plan.Operations[1]);
        var slnAdd = Assert.IsType<AddProjectToSolutionOperation>(plan.Operations[2]);

        Assert.Equal("DummyCliApi", solution.Name);
        Assert.Equal(dir, solution.Directory);
        Assert.Equal(Path.Combine(dir, "DummyCliApi.Api"), project.OutputDirectory);
        Assert.Equal(dir, slnAdd.SolutionPath); // directory — executor resolves the created file
    }

    [Theory]
    [InlineData("DummyCliApi.Api", "DummyCliApi")]
    [InlineData("Shop.Api", "Shop")]
    [InlineData("Standalone", "Standalone")]
    public void Solution_name_derives_from_project_name(string project, string expected)
    {
        Assert.Equal(expected, ProjectPlanner.DeriveSolutionName(project));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("net8.0", 8)]
    [InlineData("net10.0", 10)]
    public void Framework_major_is_parsed(string? framework, int? expected)
    {
        Assert.Equal(expected, ProjectPlanner.FrameworkMajor(framework));
    }
}

public sealed class DotnetEnvironmentTests
{
    [Fact]
    public void Sdk_list_output_parses_versions()
    {
        var versions = DotnetEnvironment.ParseSdkList(
            "8.0.404 [C:\\Program Files\\dotnet\\sdk]\n10.0.100 [C:\\Program Files\\dotnet\\sdk]\n");

        Assert.Equal(["8.0.404", "10.0.100"], versions);
    }

    [Theory]
    [InlineData(8, FrameworkSupport.SdkPresent)]
    [InlineData(9, FrameworkSupport.ViaNewerSdk)]
    [InlineData(11, FrameworkSupport.NotSupported)]
    public void Support_reflects_installed_majors(int requested, FrameworkSupport expected)
    {
        var info = new DotnetInfo(true, ["8.0.404", "10.0.100"], [8, 10]);
        Assert.Equal(expected, DotnetEnvironment.Support(info, requested));
    }

    [Fact]
    public void Template_framework_choices_are_parsed_and_sorted()
    {
        var output = "--framework <net9.0|net10.0>  net9.0 Target net9.0  net10.0 Target net10.0";

        var frameworks = DotnetEnvironment.ParseTemplateFrameworks(output);

        Assert.Equal(["net10.0", "net9.0"], frameworks);
    }
}
