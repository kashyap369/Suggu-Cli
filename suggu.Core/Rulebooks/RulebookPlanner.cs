using System.Text.RegularExpressions;
using suggu.Core.Generation;
using suggu.Core.Inspection;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Core.Rulebooks;

public static partial class RulebookPlanner
{
    public static Plan BuildPlan(
        string workspaceRoot,
        RulebookDefinition rulebook,
        RulebookCommandDefinition command,
        IReadOnlyDictionary<string, string> suppliedValues)
    {
        var root = Path.GetFullPath(workspaceRoot);
        var values = new Dictionary<string, string>(suppliedValues, StringComparer.OrdinalIgnoreCase)
        {
            ["SolutionName"] = new DirectoryInfo(root).Name,
        };
        ValidateParameters(command, values);

        var state = BuildState(root, rulebook, command, values);
        var plans = command.Actions
            .Select(action => BuildActionPlan(state, rulebook, action, values))
            .ToArray();
        var plan = Plan.Combine($"rulebook: {command.Name}", plans);
        var duplicate = plan.Operations.OfType<WriteFileOperation>()
            .GroupBy(operation => operation.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"rulebook command writes '{duplicate.Key}' more than once");
        return plan;
    }

    private static PlanningState BuildState(
        string workspaceRoot,
        RulebookDefinition rulebook,
        RulebookCommandDefinition command,
        IReadOnlyDictionary<string, string> values)
    {
        var projects = new Dictionary<string, PlannedProject>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(workspaceRoot))
        {
            foreach (var project in ProjectInspector.GetProjects(workspaceRoot))
            {
                var planned = new PlannedProject(project.Name, project.ProjectPath);
                projects[project.Name] = planned;
            }
        }

        foreach (var pair in rulebook.Projects)
        {
            var existing = projects.Values.FirstOrDefault(project =>
                project.Name.Equals(pair.Value, StringComparison.OrdinalIgnoreCase) ||
                project.Name.EndsWith('.' + pair.Value, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) projects[pair.Key] = existing;
        }

        foreach (var action in command.Actions.Where(action => NormalizeCommand(action.Command) == "create project"))
        {
            var alias = RenderRequired(action.Project, "project", values);
            var mappedName = rulebook.Projects.FirstOrDefault(pair =>
                pair.Key.Equals(alias, StringComparison.OrdinalIgnoreCase)).Value;
            var name = Render(action.Name ?? mappedName ?? alias, values).Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("create project requires a project name or alias mapping");
            var parent = ResolveWorkspacePath(workspaceRoot, Render(action.Path ?? string.Empty, values));
            var projectDirectory = Path.Combine(parent, name);
            EnsureInsideWorkspace(workspaceRoot, projectDirectory);
            var planned = new PlannedProject(name, Path.Combine(projectDirectory, $"{name}.csproj"));
            projects[alias] = planned;
            projects[name] = planned;
        }

        var solutionPath = Directory.Exists(workspaceRoot)
            ? Directory.EnumerateFiles(workspaceRoot, "*.slnx")
                .Concat(Directory.EnumerateFiles(workspaceRoot, "*.sln"))
                .FirstOrDefault()
            : null;
        var createsSolution = command.Actions.Any(action => NormalizeCommand(action.Command) == "create solution");
        return new PlanningState(workspaceRoot, projects, solutionPath ?? (createsSolution ? workspaceRoot : null));
    }

    private static Plan BuildActionPlan(
        PlanningState state,
        RulebookDefinition rulebook,
        RulebookActionDefinition action,
        IReadOnlyDictionary<string, string> values)
    {
        var command = NormalizeCommand(action.Command);
        return command switch
        {
            "create solution" => BuildSolutionPlan(state, action, values),
            "create project" => BuildProjectPlan(state, rulebook, action, values),
            "add reference" => BuildReferencePlan(state, rulebook, action, values),
            "add package" => BuildPackagePlan(state, rulebook, action, values),
            _ => BuildArtifactPlan(state, rulebook, action, values, command),
        };
    }

    private static Plan BuildSolutionPlan(
        PlanningState state,
        RulebookActionDefinition action,
        IReadOnlyDictionary<string, string> values)
    {
        var name = Render(action.Name ?? "{{SolutionName}}", values).Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("solution name cannot be empty");
        return new Plan($"create solution {name}", [new CreateSolutionOperation(state.WorkspaceRoot, name)]);
    }

    private static Plan BuildProjectPlan(
        PlanningState state,
        RulebookDefinition rulebook,
        RulebookActionDefinition action,
        IReadOnlyDictionary<string, string> values)
    {
        if (state.SolutionTarget is null)
            throw new InvalidDataException("create project requires an existing solution or an earlier create solution action");

        var project = ResolveProject(state, rulebook, RenderRequired(action.Project, "project", values));
        var projectDirectory = Path.GetDirectoryName(project.ProjectPath)!;
        var type = NormalizeProjectType(RenderRequired(action.Type, "type", values));
        var extraArguments = type == "webapi" && action.Controllers ? new[] { "--use-controllers" } : null;
        return new Plan($"create project {project.Name} ({type})",
        [
            new CreateProjectOperation(
                type,
                project.Name,
                projectDirectory,
                ProjectPlanner.NormalizeFramework(Render(action.Framework ?? string.Empty, values)),
                extraArguments),
            new AddProjectToSolutionOperation(state.SolutionTarget, project.ProjectPath),
        ]);
    }

    private static Plan BuildReferencePlan(
        PlanningState state,
        RulebookDefinition rulebook,
        RulebookActionDefinition action,
        IReadOnlyDictionary<string, string> values)
    {
        var from = ResolveProject(state, rulebook, RenderRequired(action.From, "from", values));
        var to = ResolveProject(state, rulebook, RenderRequired(action.To, "to", values));
        if (from.ProjectPath.Equals(to.ProjectPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("a rulebook reference cannot target the same project");
        return new Plan("add project reference", [new AddReferenceOperation(from.ProjectPath, to.ProjectPath)]);
    }

    private static Plan BuildPackagePlan(
        PlanningState state,
        RulebookDefinition rulebook,
        RulebookActionDefinition action,
        IReadOnlyDictionary<string, string> values)
    {
        var project = ResolveProject(state, rulebook, RenderRequired(action.Project, "project", values));
        var packageName = RenderRequired(action.Name, "name", values).Trim();
        var version = string.IsNullOrWhiteSpace(action.Version) ? null : Render(action.Version, values).Trim();
        return new Plan($"add package {packageName}",
            [new AddPackageReferenceOperation(project.ProjectPath, packageName, version)]);
    }

    private static Plan BuildArtifactPlan(
        PlanningState state,
        RulebookDefinition rulebook,
        RulebookActionDefinition action,
        IReadOnlyDictionary<string, string> values,
        string command)
    {
        var project = string.IsNullOrWhiteSpace(action.Project)
            ? null
            : ResolveProject(state, rulebook, Render(action.Project, values));
        if (project is null && command is not ("create folder" or "create file" or "add json"))
            throw new InvalidDataException($"rulebook action '{action.Command}' requires 'project'");

        ProjectContext? context = null;
        string baseDirectory;
        if (project is null)
        {
            baseDirectory = state.WorkspaceRoot;
        }
        else
        {
            var projectDirectory = Path.GetDirectoryName(project.ProjectPath)!;
            var rootNamespace = File.Exists(project.ProjectPath)
                ? ProjectLocator.ReadRootNamespace(project.ProjectPath)
                : project.Name;
            context = new ProjectContext(project.ProjectPath, projectDirectory, rootNamespace);
            baseDirectory = projectDirectory;
        }

        var relativePath = Render(action.Path ?? string.Empty, values);
        var targetDirectory = Path.GetFullPath(relativePath, baseDirectory);
        if (context is null) EnsureInsideWorkspace(state.WorkspaceRoot, targetDirectory);
        else EnsureInsideProject(context, targetDirectory);

        Plan plan = command switch
        {
            "create folder" => new Plan("create folder", [new CreateFolderOperation(targetDirectory)]),
            "create file" => BuildFilePlan(targetDirectory, RenderRequired(action.Name, "name", values), string.Empty),
            "add json" => BuildJsonPlan(targetDirectory, RenderRequired(action.Name, "name", values)),
            "add class" => CodeArtifactPlanner.BuildPlan(context!, context!.ProjectDirectory,
                RenderRequired(action.Name, "name", values), CodeArtifactType.Class, targetDirectory),
            "add interface" => CodeArtifactPlanner.BuildPlan(context!, context!.ProjectDirectory,
                RenderRequired(action.Name, "name", values), CodeArtifactType.Interface, targetDirectory),
            "add controller" => ControllerPlanner.BuildPlan(context!,
                RenderRequired(action.Name, "name", values), ParseControllerType(action.Type), targetDirectory),
            _ => throw new InvalidDataException(
                $"unsupported rulebook action '{action.Command}'. Supported: create solution, create project, create folder, create file, add class, add interface, add controller, add json, add reference, add package"),
        };

        return string.IsNullOrWhiteSpace(action.Template)
            ? plan
            : ApplyTemplate(rulebook, plan, action.Template, context, values);
    }

    private static Plan ApplyTemplate(
        RulebookDefinition rulebook,
        Plan plan,
        string templateName,
        ProjectContext? project,
        IReadOnlyDictionary<string, string> values)
    {
        var template = rulebook.Templates.FirstOrDefault(pair =>
            pair.Key.Equals(templateName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(template.Key))
            throw new InvalidDataException($"rulebook template '{templateName}' was not found");

        var writes = plan.Operations.OfType<WriteFileOperation>().ToList();
        if (writes.Count != 1)
            throw new InvalidDataException($"template '{templateName}' can only be used with an action that writes one file");

        var write = writes[0];
        var templateValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
        {
            ["TypeName"] = Path.GetFileNameWithoutExtension(write.Path),
        };
        if (project is not null)
        {
            templateValues["Namespace"] = CodeArtifactPlanner.NamespaceFor(project, Path.GetDirectoryName(write.Path)!);
            templateValues["ProjectName"] = Path.GetFileNameWithoutExtension(project.ProjectPath);
        }

        var content = Render(string.Join(Environment.NewLine, template.Value), templateValues) + Environment.NewLine;
        var operations = plan.Operations.Select(operation => ReferenceEquals(operation, write)
            ? new WriteFileOperation(write.Path, $"rulebook:{templateName}", content)
            : operation).ToList();
        return new Plan(plan.Description, operations);
    }

    private static Plan BuildFilePlan(string targetDirectory, string name, string content)
    {
        var fileName = Path.GetFileName(name.Trim());
        if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidDataException("file name cannot be empty");
        return new Plan($"create file {fileName}",
        [
            new CreateFolderOperation(targetDirectory),
            new WriteFileOperation(Path.Combine(targetDirectory, fileName), "rulebook:file", content),
        ]);
    }

    private static Plan BuildJsonPlan(string targetDirectory, string name)
    {
        var fileName = Path.GetFileName(name.Trim());
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) fileName += ".json";
        return BuildFilePlan(targetDirectory, fileName, $"{{{Environment.NewLine}}}{Environment.NewLine}");
    }

    private static PlannedProject ResolveProject(
        PlanningState state,
        RulebookDefinition rulebook,
        string aliasOrName)
    {
        if (state.Projects.TryGetValue(aliasOrName, out var planned)) return planned;

        var mapped = rulebook.Projects.FirstOrDefault(pair =>
            pair.Key.Equals(aliasOrName, StringComparison.OrdinalIgnoreCase)).Value;
        var projectName = string.IsNullOrWhiteSpace(mapped) ? aliasOrName : mapped;
        if (state.Projects.TryGetValue(projectName, out planned)) return planned;

        var existing = Directory.Exists(state.WorkspaceRoot)
            ? ProjectInspector.FindProject(state.WorkspaceRoot, projectName)
            : null;
        return existing is not null
            ? new PlannedProject(existing.Name, existing.ProjectPath)
            : throw new InvalidDataException($"rulebook project '{aliasOrName}' resolves to '{projectName}', but that project was not found or declared by create project");
    }

    private static void ValidateParameters(
        RulebookCommandDefinition command,
        IReadOnlyDictionary<string, string> values)
    {
        foreach (var parameter in command.Parameters)
        {
            if (parameter.Required && (!values.TryGetValue(parameter.Name, out var value) || string.IsNullOrWhiteSpace(value)))
                throw new InvalidDataException($"missing required parameter '{parameter.Name}'");
            if (values.TryGetValue(parameter.Name, out value) &&
                parameter.Type.Equals("csharp-identifier", StringComparison.OrdinalIgnoreCase) &&
                !CSharpIdentifierRegex().IsMatch(value))
                throw new InvalidDataException($"'{value}' is not a valid C# identifier for parameter '{parameter.Name}'");
        }
    }

    private static string NormalizeProjectType(string value) => NormalizeCommand(value) switch
    {
        "api" or "web api" or "webapi" => "webapi",
        "mvc" => "mvc",
        "console" => "console",
        "library" or "class library" or "classlib" => "classlib",
        "test" or "xunit" => "xunit",
        var other => throw new InvalidDataException($"unknown project type '{other}' - use webapi, mvc, console, classlib, or xunit"),
    };

    private static ControllerType ParseControllerType(string? value) =>
        Enum.TryParse<ControllerType>(value ?? "api", true, out var result)
            ? result
            : throw new InvalidDataException($"unknown controller type '{value}' - use api or mvc");

    private static string ResolveWorkspacePath(string workspaceRoot, string relativePath)
    {
        var resolved = Path.GetFullPath(relativePath, workspaceRoot);
        EnsureInsideWorkspace(workspaceRoot, resolved);
        return resolved;
    }

    private static void EnsureInsideWorkspace(string workspaceRoot, string path)
    {
        var relative = Path.GetRelativePath(workspaceRoot, path);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidDataException($"rulebook path '{path}' escapes workspace '{workspaceRoot}'");
    }

    private static void EnsureInsideProject(ProjectContext project, string path)
    {
        var relative = Path.GetRelativePath(project.ProjectDirectory, path);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidDataException($"rulebook path '{path}' escapes project '{Path.GetFileNameWithoutExtension(project.ProjectPath)}'");
    }

    private static string RenderRequired(string? value, string field, IReadOnlyDictionary<string, string> values) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"rulebook action requires '{field}'")
            : Render(value, values);

    private static string Render(string value, IReadOnlyDictionary<string, string> values) =>
        PlaceholderRegex().Replace(value, match =>
        {
            var key = match.Groups["name"].Value;
            return values.TryGetValue(key, out var replacement)
                ? replacement
                : throw new InvalidDataException($"unknown rulebook placeholder '{{{{{key}}}}}'");
        });

    private static string NormalizeCommand(string command) =>
        WhitespaceRegex().Replace(command.Replace('-', ' ').Trim(), " ").ToLowerInvariant();

    private sealed record PlannedProject(string Name, string ProjectPath);

    private sealed record PlanningState(
        string WorkspaceRoot,
        IReadOnlyDictionary<string, PlannedProject> Projects,
        string? SolutionTarget);

    [GeneratedRegex(@"{{\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*}}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex CSharpIdentifierRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
