namespace suggu.Core.Rulebooks;

public sealed class RulebookDefinition
{
    public string Schema { get; init; } = string.Empty;
    public Dictionary<string, string> Projects { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<RulebookCommandDefinition> Commands { get; init; } = [];
    public Dictionary<string, string[]> Templates { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RulebookCommandDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<RulebookParameterDefinition> Parameters { get; init; } = [];
    public List<RulebookActionDefinition> Actions { get; init; } = [];
}

public sealed class RulebookParameterDefinition
{
    public string Name { get; init; } = string.Empty;
    public bool Required { get; init; } = true;
    public string Type { get; init; } = "text";
}

public sealed class RulebookActionDefinition
{
    public string Command { get; init; } = string.Empty;
    public string? Project { get; init; }
    public string? Path { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Framework { get; init; }
    public string? Version { get; init; }
    public bool Controllers { get; init; }
    public string? Template { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
}

public sealed record LoadedRulebook(string FilePath, RulebookDefinition Definition);
