namespace suggu.Cli.Infrastructure;

/// <summary>
/// The single source of truth for how top-level verbs (create, list, add, info, …)
/// are grouped in "suggu --help". Command modules assign a verb's category as they
/// register it; <see cref="CategorizedHelpProvider"/> reads the same map when rendering.
/// Nothing else in the app should keep its own copy of this mapping.
/// </summary>
internal static class CommandCategories
{
    public const string Common = "Common";
    public const string CleanArchitecture = "Clean Architecture";
    public const string Inspection = "Inspection";
    public const string Diagnosis = "Diagnosis";
    public const string Other = "Other";

    /// <summary>The order categories are printed in. Anything unassigned falls into <see cref="Other"/>.</summary>
    public static readonly string[] DisplayOrder =
        [CleanArchitecture, Common, Inspection, Diagnosis, Other];

    // verb -> category, filled by command modules during Configure().
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Record which category a top-level verb belongs to.</summary>
    public static void Assign(string verb, string category) => Map[verb] = category;

    /// <summary>The category a verb was assigned, or <see cref="Other"/> if none.</summary>
    public static string Of(string verb) => Map.TryGetValue(verb, out var category) ? category : Other;
}
