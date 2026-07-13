using suggu.Core.Generation;
using suggu.Core.Workspace;

namespace suggu.Tests.Generation;

public sealed class EntityScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-scan-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LayerProject Layer => new("Domain", Path.Combine(_root, "Shop.Domain.csproj"), _root, "Shop.Domain");

    private void AddEntity(string relativePath)
    {
        var full = Path.Combine(_root, "Entities", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "// entity");
    }

    [Fact]
    public void Finds_entities_recursively_with_mirrored_parents()
    {
        AddEntity("Customer.cs");
        AddEntity(Path.Combine("Items", "Product.cs"));
        AddEntity(Path.Combine("Work", "Tasks", "Task.cs"));

        var entities = EntityScanner.Scan(Layer);

        Assert.Equal(3, entities.Count);
        Assert.Contains(new EntityRef("Customer", "", "Shop.Domain.Entities"), entities);
        Assert.Contains(new EntityRef("Product", "Items", "Shop.Domain.Entities.Items"), entities);
        Assert.Contains(new EntityRef("Task", "Work/Tasks", "Shop.Domain.Entities.Work.Tasks"), entities);
    }

    [Fact]
    public void Missing_entities_folder_returns_empty()
    {
        Assert.Empty(EntityScanner.Scan(Layer));
    }

    [Fact]
    public void File_names_with_stray_spaces_are_trimmed()
    {
        AddEntity("SystemRole .cs");

        var entity = Assert.Single(EntityScanner.Scan(Layer));
        Assert.Equal("SystemRole", entity.Name);
    }
}
