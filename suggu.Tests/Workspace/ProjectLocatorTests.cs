using suggu.Core.Workspace;

namespace suggu.Tests.Workspace;

public sealed class ProjectLocatorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-project-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Finds_project_from_nested_folder_and_reads_configured_namespace()
    {
        var projectPath = Path.Combine(_root, "Odd-Name.csproj");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><RootNamespace>Company.Product</RootNamespace></PropertyGroup>
            </Project>
            """);
        var nested = Directory.CreateDirectory(Path.Combine(_root, "Features", "Orders")).FullName;

        var project = ProjectLocator.FindProject(nested);

        Assert.NotNull(project);
        Assert.Equal(projectPath, project.ProjectPath);
        Assert.Equal("Company.Product", project.RootNamespace);
    }

    [Theory]
    [InlineData("Microsoft.NET.Sdk.Web", true)]
    [InlineData("Microsoft.NET.Sdk", false)]
    public void Detects_web_sdk_projects(string sdk, bool expected)
    {
        var projectPath = Path.Combine(_root, "WebCheck.csproj");
        File.WriteAllText(projectPath, $"<Project Sdk=\"{sdk}\" />");
        Assert.Equal(expected, suggu.Core.Inspection.ProjectInspector.IsAspNetCoreProject(projectPath));
    }
}
