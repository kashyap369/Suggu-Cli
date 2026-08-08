using suggu.Core.Inspection;

namespace suggu.Tests.Inspection;

public sealed class FileSystemFinderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-find-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Finds_files_recursively_by_partial_name()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "Features", "Orders")).FullName;
        File.WriteAllText(Path.Combine(nested, "OrderService.cs"), "class OrderService;");
        File.WriteAllText(Path.Combine(nested, "Other.txt"), "other");

        var matches = FileSystemFinder.Find(_root, "order", FileSystemEntryType.File);

        var match = Assert.Single(matches);
        Assert.Equal("OrderService.cs", match.Name);
    }

    [Fact]
    public void Supports_wildcard_folder_search()
    {
        Directory.CreateDirectory(Path.Combine(_root, "OrderHandlers"));
        Directory.CreateDirectory(Path.Combine(_root, "CustomerHandlers"));

        var matches = FileSystemFinder.Find(_root, "Order*", FileSystemEntryType.Folder);

        Assert.Single(matches);
        Assert.Equal("OrderHandlers", matches[0].Name);
    }

    [Fact]
    public void Finds_exact_file_stem_without_extension_case_insensitively()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "Api", "Controllers")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "Archive")).FullName;
        File.WriteAllText(Path.Combine(first, "BlogPostController.cs"), "first");
        File.WriteAllText(Path.Combine(second, "BLOGPOSTCONTROLLER.txt"), "second");
        File.WriteAllText(Path.Combine(second, "OldBlogPostController.cs"), "not an exact stem match");

        var matches = FileSystemFinder.FindFilesByName(_root, "blogpostcontroller");

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, path => path.EndsWith("BlogPostController.cs", StringComparison.Ordinal));
        Assert.Contains(matches, path => path.EndsWith("BLOGPOSTCONTROLLER.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void Uses_exact_full_name_when_extension_is_supplied()
    {
        File.WriteAllText(Path.Combine(_root, "Settings.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "Settings.txt"), "text");

        var matches = FileSystemFinder.FindFilesByName(_root, "settings.JSON");

        var match = Assert.Single(matches);
        Assert.EndsWith("Settings.json", match, StringComparison.Ordinal);
    }
}
