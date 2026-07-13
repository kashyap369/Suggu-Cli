using suggu.Core.Planning;

namespace suggu.Tests.Planning;

public sealed class PlanExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("suggu-executor-").FullName;
    private readonly PlanExecutor _executor = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string P(string relative) => Path.Combine(_root, relative);

    [Fact]
    public void Creates_folders_and_files()
    {
        var plan = new Plan("test", [
            new CreateFolderOperation(P("Entities")),
            new WriteFileOperation(P("Entities/User.cs"), "entity", "class User;"),
        ]);

        var report = _executor.Execute(plan, ExecutionOptions.Default);

        Assert.True(report.Success);
        Assert.Equal(2, report.Created.Count());
        Assert.Equal("class User;", File.ReadAllText(P("Entities/User.cs")));
    }

    [Fact]
    public void Skips_existing_file_by_default()
    {
        File.WriteAllText(P("User.cs"), "customized");
        var plan = new Plan("test", [new WriteFileOperation(P("User.cs"), "entity", "generated")]);

        var report = _executor.Execute(plan, ExecutionOptions.Default);

        Assert.Single(report.Skipped);
        Assert.Equal("customized", File.ReadAllText(P("User.cs")));
    }

    [Fact]
    public void Force_overwrites_existing_file()
    {
        File.WriteAllText(P("User.cs"), "old");
        var plan = new Plan("test", [new WriteFileOperation(P("User.cs"), "entity", "new")]);

        var report = _executor.Execute(plan, new ExecutionOptions(Force: true));

        Assert.Single(report.Overwritten);
        Assert.Equal("new", File.ReadAllText(P("User.cs")));
    }

    [Fact]
    public void Dry_run_reports_but_writes_nothing()
    {
        var plan = new Plan("test", [
            new CreateFolderOperation(P("Entities")),
            new WriteFileOperation(P("Entities/User.cs"), "entity", "class User;"),
        ]);

        var report = _executor.Execute(plan, new ExecutionOptions(DryRun: true));

        Assert.Equal(2, report.Created.Count());
        Assert.False(Directory.Exists(P("Entities")));
        Assert.False(File.Exists(P("Entities/User.cs")));
    }

    [Fact]
    public void Running_twice_reports_all_skipped_second_time()
    {
        var plan = new Plan("test", [
            new CreateFolderOperation(P("Entities")),
            new WriteFileOperation(P("Entities/User.cs"), "entity", "class User;"),
        ]);

        _executor.Execute(plan, ExecutionOptions.Default);
        var second = _executor.Execute(plan, ExecutionOptions.Default);

        Assert.Empty(second.Created);
        Assert.Equal(2, second.Skipped.Count());
    }
}
