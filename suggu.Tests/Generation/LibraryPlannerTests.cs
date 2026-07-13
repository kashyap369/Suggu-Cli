using suggu.Core.Generation;
using suggu.Core.Planning;

namespace suggu.Tests.Generation;

public sealed class LibraryPlannerTests
{
    private static readonly string Solution = Path.Combine("D:", "Shop", "Shop.slnx");

    [Fact]
    public void Plans_project_then_solution_add()
    {
        var plan = LibraryPlanner.BuildPlan(Solution, "Shop.Shared", framework: "9");

        var project = Assert.IsType<CreateProjectOperation>(plan.Operations[0]);
        var slnAdd = Assert.IsType<AddProjectToSolutionOperation>(plan.Operations[1]);

        Assert.Equal("classlib", project.Template);
        Assert.Equal("net9.0", project.Framework);
        Assert.Equal(Path.Combine("D:", "Shop", "Shop.Shared"), project.OutputDirectory);
        Assert.Equal(Path.Combine("D:", "Shop", "Shop.Shared", "Shop.Shared.csproj"), slnAdd.ProjectPath);
        Assert.Equal(2, plan.Operations.Count);
    }

    [Fact]
    public void Aspnet_flag_appends_framework_reference()
    {
        var plan = LibraryPlanner.BuildPlan(Solution, "Shop.Shared", aspNetCore: true);

        var frameworkRef = Assert.IsType<AddFrameworkReferenceOperation>(plan.Operations[^1]);
        Assert.Equal("Microsoft.AspNetCore.App", frameworkRef.FrameworkName);
    }

    [Fact]
    public void Framework_is_optional_and_passes_monikers_through()
    {
        Assert.Null(Assert.IsType<CreateProjectOperation>(
            LibraryPlanner.BuildPlan(Solution, "X").Operations[0]).Framework);
        Assert.Equal("net8.0", Assert.IsType<CreateProjectOperation>(
            LibraryPlanner.BuildPlan(Solution, "X", framework: "net8.0").Operations[0]).Framework);
    }
}
