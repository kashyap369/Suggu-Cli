using suggu.Core.Generation;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Tests.Generation;

public sealed class CodeArtifactPlannerTests
{
    private static readonly string Root = Path.Combine("D:", "Shop", "Shop.Api");
    private static readonly ProjectContext Project = new(
        Path.Combine(Root, "Shop.Api.csproj"), Root, "Shop.Api");

    [Fact]
    public void Class_uses_project_and_folder_namespace()
    {
        var plan = CodeArtifactPlanner.BuildPlan(
            Project, Root, "OrderService", CodeArtifactType.Class, Path.Combine(Root, "Services", "Orders"));

        var file = Assert.IsType<WriteFileOperation>(plan.Operations[1]);
        Assert.EndsWith(Path.Combine("Services", "Orders", "OrderService.cs"), file.Path);
        Assert.Contains("namespace Shop.Api.Services.Orders;", file.Content);
        Assert.Contains("public class OrderService", file.Content);
    }

    [Theory]
    [InlineData("OrderService", "IOrderService")]
    [InlineData("IOrderService", "IOrderService")]
    [InlineData("Item", "IItem")]
    public void Interface_adds_I_prefix_once(string input, string expected)
    {
        var plan = CodeArtifactPlanner.BuildPlan(Project, Root, input, CodeArtifactType.Interface);
        var file = Assert.IsType<WriteFileOperation>(plan.Operations[1]);

        Assert.EndsWith(expected + ".cs", file.Path);
        Assert.Contains($"public interface {expected}", file.Content);
    }

    [Fact]
    public void Target_cannot_escape_project()
    {
        Assert.Throws<ArgumentException>(() => CodeArtifactPlanner.BuildPlan(
            Project, Root, "Order", CodeArtifactType.Class, Path.Combine(Root, "..", "Outside")));
    }
}
