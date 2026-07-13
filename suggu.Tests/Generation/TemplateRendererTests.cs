using suggu.Core.Generation;

namespace suggu.Tests.Generation;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Replaces_known_placeholders()
    {
        var result = TemplateRenderer.Render(
            "namespace {Namespace};\n\npublic class {Name};",
            new Dictionary<string, string> { ["Namespace"] = "Shop.Domain.Entities", ["Name"] = "User" });

        Assert.Equal("namespace Shop.Domain.Entities;\n\npublic class User;", result);
    }

    [Fact]
    public void Leaves_unknown_placeholders_untouched()
    {
        var result = TemplateRenderer.Render(
            "{Name} {Mystery}",
            new Dictionary<string, string> { ["Name"] = "User" });

        Assert.Equal("User {Mystery}", result);
    }
}
