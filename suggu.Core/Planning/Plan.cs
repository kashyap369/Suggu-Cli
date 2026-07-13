namespace suggu.Core.Planning;

/// <summary>
/// An ordered list of intended operations produced by a command's planning stage.
/// Building a plan never touches the disk; only <see cref="PlanExecutor"/> does.
/// Plans compose: "setup" concatenates folder + seed plans, "new" concatenates layer plans.
/// </summary>
public sealed record Plan(string Description, IReadOnlyList<Operation> Operations)
{
    public static Plan Empty(string description) => new(description, []);

    /// <summary>Concatenate plans in order under one description.</summary>
    public static Plan Combine(string description, params Plan[] plans) =>
        new(description, plans.SelectMany(p => p.Operations).ToList());
}
