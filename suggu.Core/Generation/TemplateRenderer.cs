namespace suggu.Core.Generation;

/// <summary>
/// Plain {Placeholder} substitution — deliberately dumb until a template needs loops
/// or conditionals, at which point this becomes the seam where Scriban plugs in (v0.4).
/// </summary>
public static class TemplateRenderer
{
    /// <summary>Replace every {Key} in the template with its model value. Unknown placeholders are left as-is.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> model)
    {
        var result = template;
        foreach (var (key, value) in model)
        {
            result = result.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return result;
    }
}
