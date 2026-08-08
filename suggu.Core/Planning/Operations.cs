namespace suggu.Core.Planning;

/// <summary>
/// One intended change to the workspace. Operations are data (paths, ids, content) —
/// never delegates — so a plan can be printed, serialized, and diffed.
/// The vocabulary is closed and small on purpose; before adding a new operation type,
/// check whether it composes from existing ones.
/// </summary>
public abstract record Operation
{
    /// <summary>Workspace-relative or absolute path this operation targets.</summary>
    public abstract string TargetPath { get; }
}

/// <summary>Create a directory (parents included).</summary>
public sealed record CreateFolderOperation(string Path) : Operation
{
    public override string TargetPath => Path;
}

/// <summary>
/// Write rendered content to a file. Carries the template id it was rendered from
/// so reports can say where the content came from.
/// </summary>
public sealed record WriteFileOperation(string Path, string TemplateId, string Content) : Operation
{
    public override string TargetPath => Path;
}

/// <summary>
/// Create a project by wrapping "dotnet new <template>". Framework is the moniker
/// ("net10.0") or null for the SDK default.
/// </summary>
public sealed record CreateProjectOperation(
    string Template,
    string Name,
    string OutputDirectory,
    string? Framework,
    IReadOnlyList<string>? ExtraArguments = null) : Operation
{
    public override string TargetPath => OutputDirectory;
}

/// <summary>Create a solution file by wrapping "dotnet new sln" (.slnx on modern SDKs).</summary>
public sealed record CreateSolutionOperation(string Directory, string Name) : Operation
{
    public override string TargetPath => System.IO.Path.Combine(Directory, $"{Name} (solution)");
}

/// <summary>
/// Add a project to the solution by wrapping "dotnet sln add". SolutionPath may be
/// the solution file itself or a directory containing one (used when the solution
/// is created earlier in the same plan and its extension isn't known yet).
/// </summary>
public sealed record AddProjectToSolutionOperation(string SolutionPath, string ProjectPath) : Operation
{
    public override string TargetPath => ProjectPath;
}

/// <summary>Add a project-to-project reference by wrapping "dotnet add reference".</summary>
public sealed record AddReferenceOperation(string ProjectPath, string ReferencedProjectPath) : Operation
{
    public override string TargetPath =>
        $"{System.IO.Path.GetFileNameWithoutExtension(ProjectPath)} → {System.IO.Path.GetFileNameWithoutExtension(ReferencedProjectPath)}";
}

/// <summary>Remove a project-to-project reference through a structured csproj edit.</summary>
public sealed record RemoveReferenceOperation(string ProjectPath, string ReferencedProjectPath) : Operation
{
    public override string TargetPath =>
        $"{System.IO.Path.GetFileNameWithoutExtension(ProjectPath)} -/-> {System.IO.Path.GetFileNameWithoutExtension(ReferencedProjectPath)}";
}

/// <summary>
/// Structured csproj edit: add a &lt;FrameworkReference&gt; (e.g. Microsoft.AspNetCore.App).
/// Never regex over XML text — the executor edits the document model.
/// </summary>
public sealed record AddFrameworkReferenceOperation(string ProjectPath, string FrameworkName) : Operation
{
    public override string TargetPath => ProjectPath;
}
