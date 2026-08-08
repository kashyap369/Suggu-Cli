using suggu.Core.Generation;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Tests.Generation;

public sealed class ControllerPlannerTests
{
    private static readonly string Root = Path.Combine("D:", "Shop", "Shop.Api");
    private static readonly ProjectContext Project = new(Path.Combine(Root, "Shop.Api.csproj"), Root, "Shop.Api");

    [Fact]
    public void Api_controller_gets_suffix_namespace_and_attributes()
    {
        var plan = ControllerPlanner.BuildPlan(Project, "Orders", ControllerType.Api, Path.Combine(Root, "Controllers"));
        var file = Assert.IsType<WriteFileOperation>(plan.Operations[1]);

        Assert.EndsWith("OrdersController.cs", file.Path);
        Assert.Contains("namespace Shop.Api.Controllers;", file.Content);
        Assert.Contains("[ApiController]", file.Content);
        Assert.Contains("public class OrdersController : ControllerBase", file.Content);
    }

    [Fact]
    public void Mvc_controller_contains_index_action()
    {
        var plan = ControllerPlanner.BuildPlan(Project, "HomeController", ControllerType.Mvc, Path.Combine(Root, "Controllers"));
        var file = Assert.IsType<WriteFileOperation>(plan.Operations[1]);
        Assert.Contains("public class HomeController : Controller", file.Content);
        Assert.Contains("public IActionResult Index()", file.Content);
    }
}
