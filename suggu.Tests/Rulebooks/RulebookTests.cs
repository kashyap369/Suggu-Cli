using suggu.Core.Planning;
using suggu.Core.Rulebooks;

namespace suggu.Tests.Rulebooks;

public sealed class RulebookTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-rulebook-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Starter_markdown_contains_a_loadable_example_command()
    {
        var path = Path.Combine(_root, RulebookLoader.FileName);
        File.WriteAllText(path, RulebookTemplate.Content);

        var loaded = RulebookLoader.Load(path);

        Assert.Equal("suggu/v1", loaded.Definition.Schema);
        Assert.Equal("add entity", Assert.Single(loaded.Definition.Commands).Name);
        Assert.Contains("Supported v1 action commands", RulebookTemplate.Content);
    }

    [Fact]
    public void Composite_entity_command_plans_all_connected_files_and_custom_template()
    {
        CreateProject("Shop.Domain");
        CreateProject("Shop.Infra");
        var definition = EntityRulebook();
        var command = Assert.Single(definition.Commands);

        var plan = RulebookPlanner.BuildPlan(_root, definition, command,
            new Dictionary<string, string> { ["Name"] = "Book" });
        var writes = plan.Operations.OfType<WriteFileOperation>().ToList();

        Assert.Equal(3, writes.Count);
        Assert.Contains(writes, write => write.Path == Path.Combine(_root, "Shop.Domain", "Entities", "Book.cs"));
        Assert.Contains(writes, write => write.Path == Path.Combine(_root, "Shop.Domain", "Repositories", "IBookRepository.cs"));
        var configuration = Assert.Single(writes, write => write.Path.EndsWith("BookConfiguration.cs", StringComparison.Ordinal));
        Assert.Contains("namespace Shop.Infra.Persistence.Configurations;", configuration.Content);
        Assert.Contains("public sealed class BookConfiguration;", configuration.Content);

        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(DryRun: true));
        Assert.True(report.Success);
        Assert.False(Directory.Exists(Path.Combine(_root, "Shop.Domain", "Entities")));
    }

    [Fact]
    public void Rulebook_paths_cannot_escape_the_selected_project()
    {
        CreateProject("Shop.Domain");
        var definition = new RulebookDefinition
        {
            Schema = "suggu/v1",
            Projects = new Dictionary<string, string> { ["domain"] = "Shop.Domain" },
            Commands =
            [
                new RulebookCommandDefinition
                {
                    Name = "escape",
                    Actions =
                    [
                        new RulebookActionDefinition
                        {
                            Command = "create file",
                            Project = "domain",
                            Path = "../Outside",
                            Name = "bad.txt",
                        },
                    ],
                },
            ],
        };

        var error = Assert.Throws<InvalidDataException>(() =>
            RulebookPlanner.BuildPlan(_root, definition, definition.Commands[0],
                new Dictionary<string, string>()));

        Assert.Contains("escapes project", error.Message);
    }

    [Fact]
    public void Bootstrap_command_plans_solution_projects_packages_references_and_workspace_files()
    {
        var definition = new RulebookDefinition
        {
            Schema = "suggu/v1",
            Projects = new Dictionary<string, string>
            {
                ["api"] = "Shop.API",
                ["domain"] = "Shop.Domain",
                ["tests"] = "Shop.Domain.Tests",
            },
            Commands =
            [
                new RulebookCommandDefinition
                {
                    Name = "setup",
                    Actions =
                    [
                        new RulebookActionDefinition { Command = "create solution", Name = "Shop" },
                        new RulebookActionDefinition { Command = "create file", Name = "Directory.Build.props", Template = "build-props" },
                        new RulebookActionDefinition { Command = "create project", Project = "api", Type = "webapi", Framework = "10", Controllers = true },
                        new RulebookActionDefinition { Command = "create project", Project = "domain", Type = "classlib", Framework = "10" },
                        new RulebookActionDefinition { Command = "create project", Project = "tests", Type = "xunit", Framework = "10", Path = "tests" },
                        new RulebookActionDefinition { Command = "add package", Project = "api", Name = "Serilog.AspNetCore", Version = "10.0.0" },
                        new RulebookActionDefinition { Command = "add reference", From = "api", To = "domain" },
                        new RulebookActionDefinition { Command = "create folder", Project = "domain", Path = "Entities/Content" },
                    ],
                },
            ],
            Templates = new Dictionary<string, string[]>
            {
                ["build-props"] = ["<Project>", "  <!-- {{SolutionName}} -->", "</Project>"],
            },
        };

        var plan = RulebookPlanner.BuildPlan(_root, definition, definition.Commands[0],
            new Dictionary<string, string>());

        Assert.Single(plan.Operations.OfType<CreateSolutionOperation>());
        Assert.Equal(3, plan.Operations.OfType<CreateProjectOperation>().Count());
        Assert.Equal(3, plan.Operations.OfType<AddProjectToSolutionOperation>().Count());
        var api = Assert.Single(plan.Operations.OfType<CreateProjectOperation>(), operation => operation.Template == "webapi");
        Assert.Contains("--use-controllers", api.ExtraArguments!);
        Assert.Equal("xunit", Assert.Single(plan.Operations.OfType<CreateProjectOperation>(), operation => operation.Name.EndsWith("Tests")).Template);
        Assert.Single(plan.Operations.OfType<AddPackageReferenceOperation>());
        Assert.Single(plan.Operations.OfType<AddReferenceOperation>());
        Assert.Contains(plan.Operations.OfType<WriteFileOperation>(), operation =>
            operation.Path == Path.Combine(_root, "Directory.Build.props") && operation.Content.Contains(new DirectoryInfo(_root).Name));

        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(DryRun: true));
        Assert.True(report.Success);
        Assert.False(File.Exists(Path.Combine(_root, "Directory.Build.props")));
    }

    [Fact]
    public void Package_operation_edits_project_structurally_and_is_idempotent()
    {
        CreateProject("Shop.Api");
        var projectPath = Path.Combine(_root, "Shop.Api", "Shop.Api.csproj");
        var plan = new Plan("package",
            [new AddPackageReferenceOperation(projectPath, "MediatR", "12.5.0")]);

        var first = new PlanExecutor().Execute(plan, ExecutionOptions.Default);
        var second = new PlanExecutor().Execute(plan, ExecutionOptions.Default);

        Assert.True(first.Success);
        Assert.Equal(OperationStatus.Created, Assert.Single(first.Results).Status);
        Assert.Equal(OperationStatus.Skipped, Assert.Single(second.Results).Status);
        var content = File.ReadAllText(projectPath);
        Assert.Contains("PackageReference Include=\"MediatR\" Version=\"12.5.0\"", content);
    }

    [Fact]
    public void Package_operation_removes_template_version_for_central_management()
    {
        var directory = Directory.CreateDirectory(Path.Combine(_root, "Shop.Api"));
        var projectPath = Path.Combine(directory.FullName, "Shop.Api.csproj");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.1" />
              </ItemGroup>
            </Project>
            """);
        var plan = new Plan("central package",
            [new AddPackageReferenceOperation(projectPath, "Microsoft.AspNetCore.OpenApi")]);

        var report = new PlanExecutor().Execute(plan, ExecutionOptions.Default);

        Assert.True(report.Success);
        Assert.Equal(OperationStatus.Overwritten, Assert.Single(report.Results).Status);
        var content = File.ReadAllText(projectPath);
        Assert.Contains("PackageReference Include=\"Microsoft.AspNetCore.OpenApi\"", content);
        Assert.DoesNotContain("Version=", content);
    }

    private RulebookDefinition EntityRulebook() => new()
    {
        Schema = "suggu/v1",
        Projects = new Dictionary<string, string>
        {
            ["domain"] = "Shop.Domain",
            ["infrastructure"] = "Shop.Infra",
        },
        Commands =
        [
            new RulebookCommandDefinition
            {
                Name = "add entity",
                Parameters =
                [
                    new RulebookParameterDefinition { Name = "Name", Type = "csharp-identifier" },
                ],
                Actions =
                [
                    new RulebookActionDefinition { Command = "add class", Project = "domain", Path = "Entities", Name = "{{Name}}" },
                    new RulebookActionDefinition { Command = "add interface", Project = "domain", Path = "Repositories", Name = "{{Name}}Repository" },
                    new RulebookActionDefinition
                    {
                        Command = "add class",
                        Project = "infrastructure",
                        Path = "Persistence/Configurations",
                        Name = "{{Name}}Configuration",
                        Template = "configuration",
                    },
                ],
            },
        ],
        Templates = new Dictionary<string, string[]>
        {
            ["configuration"] =
            [
                "namespace {{Namespace}};",
                "",
                "public sealed class {{TypeName}};",
            ],
        },
    };

    private void CreateProject(string name)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_root, name));
        File.WriteAllText(Path.Combine(directory.FullName, $"{name}.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
    }
}
