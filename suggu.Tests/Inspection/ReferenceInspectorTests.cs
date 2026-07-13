using suggu.Core.Inspection;

namespace suggu.Tests.Inspection;

public sealed class ReferenceInspectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-refs-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void AddProject(string name, params string[] references)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);

        var refs = string.Join("", references.Select(r =>
            $"""<ProjectReference Include="..\{r}\{r}.csproj" />"""));
        File.WriteAllText(Path.Combine(dir, $"{name}.csproj"),
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{refs}</ItemGroup></Project>");
    }

    [Fact]
    public void Reads_direct_references_per_project()
    {
        AddProject("Shop.Domain");
        AddProject("Shop.Application", "Shop.Domain");
        AddProject("Shop.Api", "Shop.Application", "Shop.Domain");

        var graph = ReferenceInspector.GetReferenceGraph(_root);

        Assert.Equal(3, graph.Count);
        Assert.Equal([], graph.Single(p => p.Name == "Shop.Domain").References);
        Assert.Equal(["Shop.Domain"], graph.Single(p => p.Name == "Shop.Application").References);
        Assert.Equal(["Shop.Application", "Shop.Domain"], graph.Single(p => p.Name == "Shop.Api").References);
    }

    [Fact]
    public void Build_output_copies_are_ignored()
    {
        AddProject("Shop.Domain");
        var binDir = Path.Combine(_root, "Shop.Domain", "bin", "Debug");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "Shop.Domain.csproj"), "<Project />");

        Assert.Single(ReferenceInspector.GetReferenceGraph(_root));
    }
}
