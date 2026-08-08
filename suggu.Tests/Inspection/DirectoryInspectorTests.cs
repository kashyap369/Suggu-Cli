using suggu.Core.Inspection;

namespace suggu.Tests.Inspection;

public sealed class DirectoryInspectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-directory-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Overview_counts_files_folders_sizes_and_extensions()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "readme.txt"), "1234");
        File.WriteAllText(Path.Combine(_root, "src", "App.cs"), "123456");
        File.WriteAllText(Path.Combine(_root, "LICENSE"), "12");

        var overview = DirectoryInspector.GetOverview(_root);

        Assert.Equal(1, overview.FolderCount);
        Assert.Equal(3, overview.FileCount);
        Assert.Equal(12, overview.TotalBytes);
        Assert.Contains(overview.FileTypes, type => type.Extension == ".cs" && type.FileCount == 1 && type.TotalBytes == 6);
        Assert.Contains(overview.FileTypes, type => type.Extension == ".txt" && type.FileCount == 1 && type.TotalBytes == 4);
        Assert.Contains(overview.FileTypes, type => type.Extension == "(no extension)" && type.FileCount == 1);
    }

    [Fact]
    public void Tree_places_directories_before_files_and_honors_depth()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src", "Nested"));
        File.WriteAllText(Path.Combine(_root, "z.txt"), "z");

        var tree = DirectoryInspector.BuildTree(_root, maxDepth: 1);

        Assert.True(tree.Children[0].IsDirectory);
        Assert.Equal("src", tree.Children[0].Name);
        Assert.Empty(tree.Children[0].Children);
        Assert.False(tree.Children[1].IsDirectory);
    }
}
