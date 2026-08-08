using System.Text.RegularExpressions;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Core.Generation;

public enum ControllerType
{
    Api,
    Mvc,
}

public static partial class ControllerPlanner
{
    public static Plan BuildPlan(ProjectContext project, string name, ControllerType type, string targetDirectory)
    {
        var cleanName = Path.GetFileNameWithoutExtension(name.Trim());
        if (!cleanName.EndsWith("Controller", StringComparison.Ordinal)) cleanName += "Controller";
        if (!IdentifierRegex().IsMatch(cleanName))
            throw new ArgumentException($"'{cleanName}' is not a valid C# controller name", nameof(name));

        var fullTarget = Path.GetFullPath(targetDirectory);
        var nameSpace = CodeArtifactPlanner.NamespaceFor(project, fullTarget);
        var content = type == ControllerType.Api
            ? ApiTemplate(nameSpace, cleanName)
            : MvcTemplate(nameSpace, cleanName);
        var filePath = Path.Combine(fullTarget, cleanName + ".cs");
        return new Plan($"add {type.ToString().ToLowerInvariant()} controller {cleanName}",
            [new CreateFolderOperation(fullTarget), new WriteFileOperation(filePath, "built-in:controller", content)]);
    }

    private static string ApiTemplate(string nameSpace, string name) => $$"""
        using Microsoft.AspNetCore.Mvc;

        namespace {{nameSpace}};

        [ApiController]
        [Route("api/[controller]")]
        public class {{name}} : ControllerBase
        {
        }

        """;

    private static string MvcTemplate(string nameSpace, string name) => $$"""
        using Microsoft.AspNetCore.Mvc;

        namespace {{nameSpace}};

        public class {{name}} : Controller
        {
            public IActionResult Index()
            {
                return View();
            }
        }

        """;

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
