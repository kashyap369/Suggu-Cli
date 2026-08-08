using System.Text.Json;

namespace suggu.Core.Rulebooks;

public static class RulebookLoader
{
    public const string FileName = "SUGGU-RULEBOOK.md";
    public const string StartMarker = "<!-- suggu-rulebook:start -->";
    public const string EndMarker = "<!-- suggu-rulebook:end -->";

    public static string? Find(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", FileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }

    public static LoadedRulebook Load(string filePath)
    {
        var markdown = File.ReadAllText(filePath);
        var json = ExtractJson(markdown);
        RulebookDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<RulebookDefinition>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidDataException("the rulebook JSON is empty");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"invalid rulebook JSON at line {ex.LineNumber}, position {ex.BytePositionInLine}: {ex.Message}", ex);
        }

        Validate(definition);
        return new LoadedRulebook(Path.GetFullPath(filePath), definition);
    }

    public static string ExtractJson(string markdown)
    {
        var start = markdown.IndexOf(StartMarker, StringComparison.Ordinal);
        var end = markdown.IndexOf(EndMarker, StringComparison.Ordinal);
        if (start < 0 || end <= start)
            throw new InvalidDataException($"rulebook markers are missing; keep {StartMarker} and {EndMarker}");

        var section = markdown[(start + StartMarker.Length)..end];
        var fence = section.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (fence < 0) throw new InvalidDataException("the rulebook section must contain a ```json code block");
        var jsonStart = section.IndexOf('\n', fence);
        if (jsonStart < 0) throw new InvalidDataException("the rulebook JSON block has no content");
        var jsonEnd = section.IndexOf("```", jsonStart + 1, StringComparison.Ordinal);
        if (jsonEnd < 0) throw new InvalidDataException("the rulebook JSON code block is not closed");
        return section[(jsonStart + 1)..jsonEnd].Trim();
    }

    private static void Validate(RulebookDefinition definition)
    {
        if (!definition.Schema.Equals("suggu/v1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("unsupported rulebook schema; expected 'suggu/v1'");

        var duplicate = definition.Commands
            .GroupBy(command => command.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidDataException($"duplicate rulebook command '{duplicate.Key}'");

        foreach (var command in definition.Commands)
        {
            if (string.IsNullOrWhiteSpace(command.Name)) throw new InvalidDataException("rulebook command names cannot be empty");
            if (command.Actions.Count == 0) throw new InvalidDataException($"rulebook command '{command.Name}' has no actions");
            var duplicateParameter = command.Parameters
                .GroupBy(parameter => parameter.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateParameter is not null)
                throw new InvalidDataException($"command '{command.Name}' has duplicate parameter '{duplicateParameter.Key}'");
        }
    }
}
