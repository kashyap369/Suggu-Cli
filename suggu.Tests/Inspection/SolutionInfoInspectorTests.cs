using suggu.Core.Inspection;

namespace suggu.Tests.Inspection;

public sealed class SolutionInfoInspectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-info-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Reports_frameworks_dependencies_packages_size_latest_file_and_source_tree()
    {
        var solution = Path.Combine(_root, "Shop.slnx");
        File.WriteAllText(solution, "<Solution />");
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), """
            <Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """);
        File.WriteAllText(Path.Combine(_root, "Directory.Packages.props"), """
            <Project><ItemGroup><PackageVersion Include="MediatR" Version="14.0.0" /></ItemGroup></Project>
            """);
        var api = Directory.CreateDirectory(Path.Combine(_root, "Presentation", "Shop.Api")).FullName;
        var core = Directory.CreateDirectory(Path.Combine(_root, "Modules", "Shop.Core")).FullName;
        File.WriteAllText(Path.Combine(api, "Shop.Api.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <ProjectReference Include="..\..\Modules\Shop.Core\Shop.Core.csproj" />
                <PackageReference Include="MediatR" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(core, "Shop.Core.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFrameworks>net9.0;net10.0</TargetFrameworks></PropertyGroup>
            </Project>
            """);
        var source = Path.Combine(core, "Latest.cs");
        File.WriteAllText(source, "class Latest;");
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(1));
        var generated = Directory.CreateDirectory(Path.Combine(api, "bin")).FullName;
        File.WriteAllText(Path.Combine(generated, "generated.dll"), "ignored");

        var report = SolutionInfoInspector.Inspect(solution);

        Assert.Equal(2, report.Projects.Count);
        var apiInfo = Assert.Single(report.Projects, project => project.Name == "Shop.Api");
        Assert.Equal(["net10.0"], apiInfo.TargetFrameworks);
        Assert.Equal(["Shop.Core"], apiInfo.References);
        Assert.Equal("14.0.0", Assert.Single(apiInfo.Packages).Version);
        var coreInfo = Assert.Single(report.Projects, project => project.Name == "Shop.Core");
        Assert.Equal(["net10.0", "net9.0"], coreInfo.TargetFrameworks);
        Assert.Equal(source, report.LatestModifiedFile);
        Assert.DoesNotContain(Flatten(report.Tree), entry => entry.Name.Equals("bin", StringComparison.OrdinalIgnoreCase));
        Assert.True(report.TotalBytes > 0);
    }

    private static IEnumerable<DirectoryEntry> Flatten(DirectoryEntry entry)
    {
        yield return entry;
        foreach (var child in entry.Children.SelectMany(Flatten)) yield return child;
    }
}
