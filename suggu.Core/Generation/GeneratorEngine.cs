using suggu.Core.Packs;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Core.Generation;

/// <summary>
/// The one generic engine behind every add-style command. It knows nothing about
/// entities or CQRS: it reads a generator recipe from the pack, renders its templates,
/// and emits a plan. New scaffold shapes are pack entries, never new engine code.
/// </summary>
public static class GeneratorEngine
{
    /// <summary>
    /// Build the plan for one generator invocation. No disk writes happen here —
    /// the caller hands the plan to <see cref="PlanExecutor"/>.
    /// </summary>
    public static Plan BuildPlan(
        GeneratorSpec generator,
        GeneratorInput input,
        LayerProject targetLayer,
        IPackFileProvider packFiles)
    {
        return Build($"add {generator.Name} {Combine(input)}", generator.Outputs, input, targetLayer, packFiles);
    }

    /// <summary>
    /// The plan for one setup section. Seeds are generators without user input:
    /// fixed paths, same rendering, same executor.
    /// </summary>
    public static Plan BuildSeedPlan(SeedSpec seed, LayerProject targetLayer, IPackFileProvider packFiles)
    {
        return Build($"setup {seed.Layer} {seed.Section}", seed.Outputs, null, targetLayer, packFiles);
    }

    /// <summary>
    /// The plan for a scan-mode generator (add repositories): shared outputs once
    /// (the IRepository&lt;T&gt; base), then the per-entity outputs, mirroring each
    /// entity's subfolder. Scanned templates additionally see {SourceNamespace} —
    /// the namespace the scanned entity lives in.
    /// </summary>
    public static Plan BuildScanPlan(
        GeneratorSpec generator,
        IReadOnlyList<EntityRef> entities,
        LayerProject targetLayer,
        IPackFileProvider packFiles)
    {
        var operations = new List<Operation>();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var output in generator.SharedOutputsOrEmpty)
        {
            Emit(operations, seenFolders, output, null, null, targetLayer, packFiles);
        }

        foreach (var entity in entities)
        {
            var input = new GeneratorInput(entity.Name, entity.Parent);
            var extra = new Dictionary<string, string> { ["SourceNamespace"] = entity.Namespace };

            foreach (var output in generator.Outputs)
            {
                Emit(operations, seenFolders, output, input, extra, targetLayer, packFiles);
            }
        }

        return new Plan($"add {generator.Name} ({entities.Count} entities)", operations);
    }

    private static Plan Build(
        string description,
        IReadOnlyList<GeneratorOutput> outputs,
        GeneratorInput? input,
        LayerProject targetLayer,
        IPackFileProvider packFiles)
    {
        var operations = new List<Operation>();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var output in outputs)
        {
            Emit(operations, seenFolders, output, input, null, targetLayer, packFiles);
        }

        return new Plan(description, operations);
    }

    /// <summary>Expand one output into its folder + file operations.</summary>
    private static void Emit(
        List<Operation> operations,
        HashSet<string> seenFolders,
        GeneratorOutput output,
        GeneratorInput? input,
        IReadOnlyDictionary<string, string>? extraModel,
        LayerProject targetLayer,
        IPackFileProvider packFiles)
    {
        var relativePath = ExpandPathPattern(output.PathPattern, input);
        var fullPath = Path.Combine(targetLayer.Directory, relativePath);

        // Folders before files: emit each new parent folder once.
        var folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(folder) && seenFolders.Add(folder))
        {
            operations.Add(new CreateFolderOperation(folder));
        }

        var model = BuildModel(input, targetLayer, relativePath);
        if (extraModel is not null)
        {
            foreach (var (key, value) in extraModel)
            {
                model[key] = value;
            }
        }

        var template = packFiles.ReadText(output.Template);
        var content = TemplateRenderer.Render(template, model);

        operations.Add(new WriteFileOperation(fullPath, output.Template, content));
    }

    /// <summary>"Entities/{Parent}/{Name}.cs" with empty Parent collapses to "Entities/User.cs".</summary>
    private static string ExpandPathPattern(string pattern, GeneratorInput? input)
    {
        var expanded = input is null
            ? pattern
            : pattern
                .Replace("{Name}", input.Name, StringComparison.Ordinal)
                .Replace("{Parent}", input.Parent, StringComparison.Ordinal);

        var segments = expanded.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    private static Dictionary<string, string> BuildModel(
        GeneratorInput? input, LayerProject targetLayer, string relativePath)
    {
        // Namespace mirrors the folder the file lands in: Shop.Domain + Entities/Worker -> Shop.Domain.Entities.Worker
        var folder = Path.GetDirectoryName(relativePath) ?? string.Empty;
        var namespaceSuffix = folder
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace('/', '.');

        var ns = string.IsNullOrEmpty(namespaceSuffix)
            ? targetLayer.RootNamespace
            : $"{targetLayer.RootNamespace}.{namespaceSuffix}";

        var model = new Dictionary<string, string>
        {
            ["Namespace"] = ns,
            ["RootNamespace"] = targetLayer.RootNamespace,
        };

        if (input is not null)
        {
            model["Name"] = input.Name;
            model["Parent"] = input.Parent;
        }

        return model;
    }

    private static string Combine(GeneratorInput input) =>
        input.Parent.Length == 0 ? input.Name : $"{input.Parent}/{input.Name}";
}
