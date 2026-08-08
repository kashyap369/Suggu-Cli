namespace suggu.Core.Planning;

/// <summary>How the executor applies a plan. Defaults: real run, never overwrite.</summary>
public sealed record ExecutionOptions(bool DryRun = false, bool Force = false)
{
    public static readonly ExecutionOptions Default = new();
}

/// <summary>
/// The only code in suggu that touches the disk. Every command builds a Plan and
/// hands it here; skip/overwrite/dry-run/reporting behavior therefore exists in
/// exactly one place. File writes or process starts anywhere else are a bug.
/// </summary>
public sealed class PlanExecutor
{
    /// <summary>
    /// Apply the plan in order. Existing files are skipped unless <c>Force</c>;
    /// a failed operation stops the run so the report shows exactly what was and wasn't done.
    /// </summary>
    public ExecutionReport Execute(Plan plan, ExecutionOptions options)
    {
        var results = new List<OperationResult>();

        foreach (var operation in plan.Operations)
        {
            var result = Apply(operation, options);
            results.Add(result);

            if (result.Status == OperationStatus.Failed)
            {
                break;
            }
        }

        return new ExecutionReport(plan.Description, options.DryRun, results);
    }

    private static OperationResult Apply(Operation operation, ExecutionOptions options)
    {
        try
        {
            return operation switch
            {
                CreateFolderOperation folder => ApplyCreateFolder(folder, options),
                WriteFileOperation file => ApplyWriteFile(file, options),
                CreateProjectOperation project => ApplyCreateProject(project, options),
                CreateSolutionOperation solution => ApplyCreateSolution(solution, options),
                AddProjectToSolutionOperation solutionAdd => ApplyAddProjectToSolution(solutionAdd, options),
                AddReferenceOperation reference => ApplyAddReference(reference, options),
                RemoveReferenceOperation reference => ApplyRemoveReference(reference, options),
                AddFrameworkReferenceOperation frameworkRef => ApplyAddFrameworkReference(frameworkRef, options),
                _ => new OperationResult(operation, OperationStatus.Failed, $"unknown operation type {operation.GetType().Name}"),
            };
        }
        catch (Exception ex)
        {
            return new OperationResult(operation, OperationStatus.Failed, ex.Message);
        }
    }

    private static OperationResult ApplyCreateFolder(CreateFolderOperation op, ExecutionOptions options)
    {
        if (Directory.Exists(op.Path))
        {
            return new OperationResult(op, OperationStatus.Skipped, "folder already exists");
        }

        if (!options.DryRun)
        {
            Directory.CreateDirectory(op.Path);
        }

        return new OperationResult(op, OperationStatus.Created);
    }

    private static OperationResult ApplyWriteFile(WriteFileOperation op, ExecutionOptions options)
    {
        var exists = File.Exists(op.Path);

        if (exists && !options.Force)
        {
            return new OperationResult(op, OperationStatus.Skipped, "file already exists");
        }

        if (!options.DryRun)
        {
            var parent = Path.GetDirectoryName(op.Path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(op.Path, op.Content);
        }

        return new OperationResult(op, exists ? OperationStatus.Overwritten : OperationStatus.Created);
    }

    private static OperationResult ApplyCreateProject(CreateProjectOperation op, ExecutionOptions options)
    {
        if (Directory.Exists(op.OutputDirectory) &&
            Directory.EnumerateFiles(op.OutputDirectory, "*.csproj").Any())
        {
            return new OperationResult(op, OperationStatus.Skipped, "project already exists");
        }

        if (options.DryRun)
        {
            return new OperationResult(op, OperationStatus.Created);
        }

        var args = new List<string> { "new", op.Template, "-n", op.Name, "-o", op.OutputDirectory };
        if (op.Framework is not null)
        {
            args.AddRange(["-f", op.Framework]);
        }
        if (op.ExtraArguments is not null)
        {
            args.AddRange(op.ExtraArguments);
        }

        var result = ProcessRunner.Run("dotnet", args);
        return result.Success
            ? new OperationResult(op, OperationStatus.Created)
            : new OperationResult(op, OperationStatus.Failed, result.Output);
    }

    private static OperationResult ApplyCreateSolution(CreateSolutionOperation op, ExecutionOptions options)
    {
        if (FindSolutionFileIn(op.Directory, op.Name) is not null)
        {
            return new OperationResult(op, OperationStatus.Skipped, "solution already exists");
        }

        if (options.DryRun)
        {
            return new OperationResult(op, OperationStatus.Created);
        }

        var result = ProcessRunner.Run("dotnet", ["new", "sln", "-n", op.Name, "-o", op.Directory]);
        return result.Success
            ? new OperationResult(op, OperationStatus.Created)
            : new OperationResult(op, OperationStatus.Failed, result.Output);
    }

    private static OperationResult ApplyAddProjectToSolution(AddProjectToSolutionOperation op, ExecutionOptions options)
    {
        // SolutionPath may be a directory when the solution was created earlier in this plan.
        var solutionFile = ResolveSolutionFile(op.SolutionPath);

        // Idempotency: the solution file already names this project -> nothing to do.
        var projectFileName = Path.GetFileName(op.ProjectPath);
        if (solutionFile is not null &&
            File.ReadAllText(solutionFile).Contains(projectFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(op, OperationStatus.Skipped, "already in solution");
        }

        if (options.DryRun)
        {
            return new OperationResult(op, OperationStatus.Created);
        }

        if (solutionFile is null)
        {
            return new OperationResult(op, OperationStatus.Failed, $"no solution found at {op.SolutionPath}");
        }

        var result = ProcessRunner.Run("dotnet", ["sln", solutionFile, "add", op.ProjectPath]);
        return result.Success
            ? new OperationResult(op, OperationStatus.Created)
            : new OperationResult(op, OperationStatus.Failed, result.Output);
    }

    private static string? ResolveSolutionFile(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*.slnx")
                .Concat(Directory.EnumerateFiles(path, "*.sln"))
                .FirstOrDefault();
        }

        return null;
    }

    private static string? FindSolutionFileIn(string directory, string name)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var slnx = Path.Combine(directory, $"{name}.slnx");
        var sln = Path.Combine(directory, $"{name}.sln");
        return File.Exists(slnx) ? slnx : File.Exists(sln) ? sln : null;
    }

    private static OperationResult ApplyAddReference(AddReferenceOperation op, ExecutionOptions options)
    {
        if (!File.Exists(op.ProjectPath))
        {
            return new OperationResult(op, OperationStatus.Failed, $"project not found: {op.ProjectPath}");
        }

        // Idempotency: the csproj already references a project with this file name.
        var referencedFileName = Path.GetFileName(op.ReferencedProjectPath);
        var doc = System.Xml.Linq.XDocument.Load(op.ProjectPath);
        var exists = doc.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Any(include => include is not null &&
                Path.GetFileName(include.Replace('\\', '/')).Equals(referencedFileName, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            return new OperationResult(op, OperationStatus.Skipped, "reference already present");
        }

        if (options.DryRun)
        {
            return new OperationResult(op, OperationStatus.Created);
        }

        var result = ProcessRunner.Run("dotnet", ["add", op.ProjectPath, "reference", op.ReferencedProjectPath]);
        return result.Success
            ? new OperationResult(op, OperationStatus.Created)
            : new OperationResult(op, OperationStatus.Failed, result.Output);
    }

    private static OperationResult ApplyAddFrameworkReference(AddFrameworkReferenceOperation op, ExecutionOptions options)
    {
        if (!File.Exists(op.ProjectPath))
        {
            // Dry-run: the csproj this edits would be created earlier in the same plan.
            return options.DryRun
                ? new OperationResult(op, OperationStatus.Created)
                : new OperationResult(op, OperationStatus.Failed, "project file not found");
        }

        var doc = System.Xml.Linq.XDocument.Load(op.ProjectPath);
        var exists = doc.Descendants("FrameworkReference")
            .Any(e => string.Equals((string?)e.Attribute("Include"), op.FrameworkName, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            return new OperationResult(op, OperationStatus.Skipped, "framework reference already present");
        }

        if (!options.DryRun)
        {
            var itemGroup = new System.Xml.Linq.XElement("ItemGroup",
                new System.Xml.Linq.XElement("FrameworkReference",
                    new System.Xml.Linq.XAttribute("Include", op.FrameworkName)));
            doc.Root!.Add(itemGroup);
            doc.Save(op.ProjectPath);
        }

        return new OperationResult(op, OperationStatus.Created);
    }

    private static OperationResult ApplyRemoveReference(RemoveReferenceOperation op, ExecutionOptions options)
    {
        if (!File.Exists(op.ProjectPath))
        {
            return new OperationResult(op, OperationStatus.Failed, $"project not found: {op.ProjectPath}");
        }

        var document = System.Xml.Linq.XDocument.Load(op.ProjectPath);
        var referencedFileName = Path.GetFileName(op.ReferencedProjectPath);
        var references = document.Descendants("ProjectReference")
            .Where(element =>
            {
                var include = (string?)element.Attribute("Include");
                return include is not null &&
                    Path.GetFileName(include.Replace('\\', '/')).Equals(referencedFileName, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        if (references.Count == 0)
        {
            return new OperationResult(op, OperationStatus.Skipped, "reference not present");
        }

        if (!options.DryRun)
        {
            foreach (var reference in references) reference.Remove();
            document.Save(op.ProjectPath);
        }

        return new OperationResult(op, OperationStatus.Deleted);
    }
}
