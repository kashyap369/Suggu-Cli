namespace suggu.Core.Packs;

/// <summary>
/// The pack schema — the contract between the generic engine and the data that
/// describes one architecture profile. Nothing about "clean architecture" is
/// hardcoded in the engine; the pack is law. schemaVersion is stamped from day one
/// so old packs fail loudly, never silently.
/// </summary>
public sealed record PackManifest
{
    public required string SchemaVersion { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<LayerSpec> Layers { get; init; } = [];
    public IReadOnlyList<GeneratorSpec> Generators { get; init; } = [];
    public IReadOnlyList<SeedSpec> Seeds { get; init; } = [];

    public GeneratorSpec? FindGenerator(string name) =>
        Generators.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    public LayerSpec? FindLayer(string name) =>
        Layers.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Seeds for one layer, optionally narrowed to a single named section.</summary>
    public IReadOnlyList<SeedSpec> FindSeeds(string layer, string? section = null) =>
        Seeds
            .Where(s => string.Equals(s.Layer, layer, StringComparison.OrdinalIgnoreCase))
            .Where(s => section is null || string.Equals(s.Section, section, StringComparison.OrdinalIgnoreCase))
            .ToList();
}

/// <summary>
/// One architectural layer: how to detect its project (e.g. "*.Domain")
/// and how it appears in help ("Domain Layer Commands").
/// </summary>
public sealed record LayerSpec(
    string Name,
    IReadOnlyList<string> ProjectPatterns);

/// <summary>Where a generator's input comes from.</summary>
public enum GeneratorMode
{
    /// <summary>The user passes a name on the command line (suggu add entity Worker/User).</summary>
    Input,

    /// <summary>The input is a scan of Entities/** — one output set per entity found (add repositories).</summary>
    EntityScan,
}

/// <summary>
/// A named scaffold recipe — the mechanism behind every "suggu add ..." command.
/// A generator is a pack entry, not a C# command: adding one next month means
/// editing pack data plus templates, zero new tool code.
/// </summary>
public sealed record GeneratorSpec(
    string Name,
    string Description,
    string Layer,
    IReadOnlyList<GeneratorOutput> Outputs,
    GeneratorMode Mode = GeneratorMode.Input,
    IReadOnlyList<GeneratorOutput>? SharedOutputs = null)
{
    /// <summary>Outputs rendered once per run (not per entity/input) — e.g. the IRepository&lt;T&gt; base.</summary>
    public IReadOnlyList<GeneratorOutput> SharedOutputsOrEmpty => SharedOutputs ?? [];
}

/// <summary>
/// One file a generator emits: a path pattern (relative to the target layer's project,
/// with {Name}/{Parent} placeholders) plus the template that renders its content.
/// </summary>
public sealed record GeneratorOutput(
    string PathPattern,
    string Template);

/// <summary>
/// One "setup" section: the seed files that give a layer its canonical shape
/// (e.g. Domain/common -> BaseEntity, AuditableEntity, IAggregateRoot, Result).
/// Seed outputs take no Name/Parent input — their paths are fixed by the pack.
/// </summary>
public sealed record SeedSpec(
    string Layer,
    string Section,
    string Description,
    IReadOnlyList<GeneratorOutput> Outputs);
