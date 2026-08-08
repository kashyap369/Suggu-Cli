# Suggu architecture

## Product boundary

Suggu is a global .NET 10 tool with two command groups rendered separately by root help:

- **General commands** operate on the filesystem from any directory.
- **.NET solution/project commands** discover and validate `.sln`, `.slnx`, and `.csproj` context.

There are no architecture-profile or fixed layer-name assumptions. The word “layer” in CLI options is a user-friendly alias for selecting an ordinary project by short or full name.

## Projects

| Project | Responsibility |
| --- | --- |
| `suggu.Cli` | Spectre command grammar, interactive selection/confirmation, context validation, category-aware help, and rendering. |
| `suggu.Core` | Reusable filesystem search/inspection, project discovery, planning/execution, templates, project/reference operations, build diagnostics, and endpoint tracing. |
| `suggu.Tests` | xUnit coverage for Core behavior. |

## Important components

- `CommandCategories` maps full command paths such as `create folder` and `create project`, allowing one verb branch to appear under different root-help categories.
- `ProjectCommandResolver` handles project selection from path, `--layer`, current project, or an interactive arrow-key prompt.
- `SolutionLocator` and `ProjectLocator` walk upward for solution/project context and derive root namespaces.
- `PlanExecutor` centralizes idempotent files/projects/references, dry-run results, and structured reference removal.
- `DirectoryInspector` produces trees and aggregate counts/sizes; `FileSystemFinder` provides recursive partial/wildcard search and case-insensitive exact filename/stem lookup for previews.
- `CodeArtifactPlanner` and `ControllerPlanner` generate generic C# templates and folder-derived namespaces.
- `ProjectPlanner` maps API, MVC, and console project requests to official `dotnet new` templates and composes optional solution creation/addition.
- `DotnetEnvironment` reads installed SDKs and each active template's accepted target frameworks for the interactive creation wizard. `WorkspaceLauncher` opens a completed solution/project through the operating-system association only after explicit option or interactive confirmation.
- `ProjectInspector` reads ordinary projects/packages and detects ASP.NET Core Web projects.
- `SolutionInfoInspector` aggregates frameworks, references, packages, source-focused sizes, latest changes, and a filtered source tree for `project info`; `ProjectInspector` resolves standard `Directory.Build.props` framework inheritance and `Directory.Packages.props` central versions.
- `ReferenceInspector`, `BuildInspector`, and `EndpointFlowInspector` provide solution diagnostics.

## Safety model

Existing generated source is skipped unless `--force` is explicit. Filesystem deletion is isolated under `remove file` and `remove folder`; creation commands never delete. Removal reports that Suggu cannot recover data, and folder removal refuses filesystem roots or the selected parent itself. Reference removal remains an explicit `add references --remove` operation, edits XML structurally, and skips absent references.

## Flow and preview limitations

Endpoint tracing is recursive and folder/architecture neutral, but remains conservative static analysis rather than runtime tracing. Its tree contains only methods resolved to non-test workspace source files; framework/package calls are omitted. It handles interface or concrete dependencies, ordinary field naming, partial controller files, common local variables, Dapper generic row types, LINQ lambda parameters, and C# type aliases. It cannot guarantee middleware/filter/reflection/runtime-DI paths. Grep previews known text formats; binary formats are not decoded and instead return an absolute external-viewer path.

## Legacy material

`SUGGU-CLI-PLAN.pdf` and `tree.json` are historical research artifacts and do not define current behavior.
