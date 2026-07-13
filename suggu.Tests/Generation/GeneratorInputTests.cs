using suggu.Core.Generation;

namespace suggu.Tests.Generation;

public sealed class GeneratorInputTests
{
    [Fact]
    public void Plain_name_has_no_parent()
    {
        var input = GeneratorInput.Parse("User");
        Assert.Equal("User", input.Name);
        Assert.Equal(string.Empty, input.Parent);
    }

    [Fact]
    public void Slash_splits_parent_and_name()
    {
        var input = GeneratorInput.Parse("Worker/User");
        Assert.Equal("User", input.Name);
        Assert.Equal("Worker", input.Parent);
    }

    [Fact]
    public void Deep_nesting_keeps_full_parent_path()
    {
        var input = GeneratorInput.Parse("Hr/Worker/User");
        Assert.Equal("User", input.Name);
        Assert.Equal("Hr/Worker", input.Parent);
    }

    [Fact]
    public void Backslashes_are_normalized()
    {
        var input = GeneratorInput.Parse(@"Worker\User");
        Assert.Equal("User", input.Name);
        Assert.Equal("Worker", input.Parent);
    }

    [Fact]
    public void Empty_input_throws()
    {
        Assert.Throws<ArgumentException>(() => GeneratorInput.Parse("/"));
    }
}
