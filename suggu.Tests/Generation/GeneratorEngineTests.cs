using suggu.Core.Generation;
using suggu.Core.Packs;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Tests.Generation;

public sealed class GeneratorEngineTests
{
    private sealed class InMemoryPackFiles(Dictionary<string, string> files) : IPackFileProvider
    {
        public string ReadText(string relativePath) => files[relativePath];
        public bool Exists(string relativePath) => files.ContainsKey(relativePath);
    }

    private static readonly LayerProject Domain = new(
        "Domain",
        Path.Combine("D:", "Shop", "Shop.Domain", "Shop.Domain.csproj"),
        Path.Combine("D:", "Shop", "Shop.Domain"),
        "Shop.Domain");

    private static readonly IPackFileProvider Files = new InMemoryPackFiles(new()
    {
        ["templates/entity.txt"] = "namespace {Namespace};\n\npublic class {Name};",
    });

    private static readonly GeneratorSpec Entity = new(
        "entity", "Create an entity", "Domain",
        [new GeneratorOutput("Entities/{Parent}/{Name}.cs", "templates/entity.txt")]);

    [Fact]
    public void Nested_name_creates_subfolder_and_namespaced_file()
    {
        var plan = GeneratorEngine.BuildPlan(Entity, GeneratorInput.Parse("Worker/User"), Domain, Files);

        var folder = Assert.IsType<CreateFolderOperation>(plan.Operations[0]);
        var file = Assert.IsType<WriteFileOperation>(plan.Operations[1]);

        Assert.Equal(Path.Combine(Domain.Directory, "Entities", "Worker"), folder.Path);
        Assert.Equal(Path.Combine(Domain.Directory, "Entities", "Worker", "User.cs"), file.Path);
        Assert.Equal("namespace Shop.Domain.Entities.Worker;\n\npublic class User;", file.Content);
    }

    [Fact]
    public void Plain_name_collapses_the_empty_parent_segment()
    {
        var plan = GeneratorEngine.BuildPlan(Entity, GeneratorInput.Parse("User"), Domain, Files);

        var file = Assert.IsType<WriteFileOperation>(plan.Operations[1]);
        Assert.Equal(Path.Combine(Domain.Directory, "Entities", "User.cs"), file.Path);
        Assert.Contains("namespace Shop.Domain.Entities;", file.Content);
    }

    [Fact]
    public void Multi_output_generator_emits_each_folder_once()
    {
        var files = new InMemoryPackFiles(new()
        {
            ["templates/a.txt"] = "A {Name}",
            ["templates/b.txt"] = "B {Name}",
        });
        var generator = new GeneratorSpec("command", "CQRS command", "Application",
        [
            new GeneratorOutput("Features/{Parent}/Commands/{Name}/{Name}Command.cs", "templates/a.txt"),
            new GeneratorOutput("Features/{Parent}/Commands/{Name}/{Name}Handler.cs", "templates/b.txt"),
        ]);

        var plan = GeneratorEngine.BuildPlan(generator, GeneratorInput.Parse("User/CreateUser"), Domain, files);

        // One shared folder + two files, not two folder ops.
        Assert.Equal(3, plan.Operations.Count);
        Assert.Single(plan.Operations.OfType<CreateFolderOperation>());
        Assert.Equal(2, plan.Operations.OfType<WriteFileOperation>().Count());
    }

    [Fact]
    public void Scan_plan_emits_shared_base_once_and_mirrors_entity_subfolders()
    {
        var files = new InMemoryPackFiles(new()
        {
            ["templates/base.txt"] = "namespace {Namespace};\n\npublic interface IRepository<T>;",
            ["templates/repo.txt"] = "using {Name} = {SourceNamespace}.{Name};\n\nnamespace {Namespace};\n\npublic interface I{Name}Repository : IRepository<{Name}>;",
        });
        var generator = new GeneratorSpec(
            "repositories", "mirrored interfaces", "Domain",
            [new GeneratorOutput("Interfaces/{Parent}/I{Name}Repository.cs", "templates/repo.txt")],
            GeneratorMode.EntityScan,
            [new GeneratorOutput("Interfaces/IRepository.cs", "templates/base.txt")]);
        var entities = new List<EntityRef>
        {
            new("Customer", "", "Shop.Domain.Entities"),
            new("Product", "Items", "Shop.Domain.Entities.Items"),
        };

        var plan = GeneratorEngine.BuildScanPlan(generator, entities, Domain, files);

        var writes = plan.Operations.OfType<WriteFileOperation>().ToList();
        Assert.Equal(3, writes.Count); // base + 2 interfaces

        Assert.Equal(Path.Combine(Domain.Directory, "Interfaces", "IRepository.cs"), writes[0].Path);
        Assert.Contains("namespace Shop.Domain.Interfaces;", writes[0].Content);

        var product = writes.Single(w => w.Path.EndsWith("IProductRepository.cs", StringComparison.Ordinal));
        Assert.Equal(Path.Combine(Domain.Directory, "Interfaces", "Items", "IProductRepository.cs"), product.Path);
        Assert.Contains("using Product = Shop.Domain.Entities.Items.Product;", product.Content);
        Assert.Contains("namespace Shop.Domain.Interfaces.Items;", product.Content);
        Assert.Contains("public interface IProductRepository : IRepository<Product>;", product.Content);
    }

    [Fact]
    public void Seed_plan_renders_fixed_paths_with_layer_namespace()
    {
        var files = new InMemoryPackFiles(new()
        {
            ["templates/base.txt"] = "namespace {Namespace};\n\npublic abstract class BaseEntity;",
        });
        var seed = new SeedSpec("Domain", "common", "base types",
            [new GeneratorOutput("Common/BaseEntity.cs", "templates/base.txt")]);

        var plan = GeneratorEngine.BuildSeedPlan(seed, Domain, files);

        var file = Assert.IsType<WriteFileOperation>(plan.Operations[1]);
        Assert.Equal(Path.Combine(Domain.Directory, "Common", "BaseEntity.cs"), file.Path);
        Assert.Contains("namespace Shop.Domain.Common;", file.Content);
    }
}
