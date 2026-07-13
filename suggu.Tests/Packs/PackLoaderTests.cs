using suggu.Core.Packs;

namespace suggu.Tests.Packs;

public sealed class PackLoaderTests
{
    [Fact]
    public void Default_pack_loads_from_embedded_resources()
    {
        var (manifest, _) = PackLoader.LoadDefault();

        Assert.Equal("clean-webapi", manifest.Name);
        Assert.Equal(PackLoader.SupportedSchemaVersion, manifest.SchemaVersion);
        Assert.Contains(manifest.Layers, l => l.Name == "Domain");
        Assert.Contains(manifest.Layers, l => l.Name == "Application");
        Assert.Contains(manifest.Layers, l => l.Name == "Infrastructure");
        Assert.Contains(manifest.Layers, l => l.Name == "Api");
    }

    [Fact]
    public void Wrong_schema_version_fails_loudly()
    {
        var dir = Directory.CreateTempSubdirectory("suggu-pack-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "pack.json"),
                """{ "schemaVersion": "999", "name": "old", "description": "", "layers": [], "generators": [] }""");

            var ex = Assert.Throws<InvalidOperationException>(() => PackLoader.LoadFromDirectory(dir));
            Assert.Contains("schema version", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
