namespace suggu.Cli.Infrastructure;

internal static class CommandCategories
{
    public const string General = "General commands";
    public const string Dotnet = ".NET solution/project commands";
    public const string Other = "Other";

    public static readonly string[] DisplayOrder = [General, Dotnet, Other];
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase);

    public static void Assign(string commandPath, string category) => Map[commandPath] = category;
    public static string Of(string commandPath) => Map.TryGetValue(commandPath, out var category) ? category : Other;
}
