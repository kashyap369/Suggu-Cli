# Suggu CLI

Suggu is a global .NET productivity tool for filesystem work and ordinary .NET solutions. It creates and inspects projects, source artifacts, references, builds, endpoint flows, packages, and folder structures without assuming Clean Architecture or fixed layer names.

- Current version: `0.4.0`
- Tool runtime: `.NET 10`
- UI: `Spectre.Console`

## Core rules

- General commands work from any directory.
- .NET commands discover an enclosing `.sln`, `.slnx`, or `.csproj`.
- Interactive terminals provide arrow-key project, project-type, and framework selection.
- Source-artifact paths are relative to the selected project, such as `Features/Orders/Commands`; absolute drive paths are unnecessary.
- Existing source files are preserved unless `--force` is explicit.
- Creation commands never delete. Filesystem deletion exists only under `suggu remove`.
- Suggu has no Domain/Application/Infrastructure naming requirement and no hard-coded architecture profile.

## Build and install

Requirements:

- .NET 10 SDK
- Windows, Linux, or macOS terminal

```powershell
git clone https://github.com/kashyap369/Suggu-Cli.git
cd Suggu-Cli
dotnet restore SugguCli.slnx
dotnet build SugguCli.slnx -c Release
dotnet test SugguCli.slnx -c Release
dotnet pack suggu.Cli/suggu.Cli.csproj -c Release -o nupkg
dotnet tool install --global suggu --version 0.4.0 --add-source ./nupkg
```

Update an existing installation:

```powershell
dotnet tool update --global suggu --version 0.4.0 --add-source ./nupkg
```

Verify or uninstall:

```powershell
suggu --version
suggu --help
dotnet tool uninstall --global suggu
```

## Interactive project creation

```powershell
suggu create project
```

The wizard asks for:

1. Project type: Web API, MVC, or Console.
2. Project name.
3. A target framework accepted by the active installed `dotnet new` template.
4. Whether to open the completed solution/project in the system-associated IDE.

Explicit forms remain supported:

```powershell
suggu create project webapi 10 -n Shop.Api --controllers
suggu create project mvc 10 -n Shop.Web -p ./src
suggu create project console 9 -n Shop.Tools --no-sln
suggu create project --type console --framework 10 --name Shop.Worker --open
```

Use `--dry-run` to preview project and solution operations.

## General commands

### Create folders and files

```powershell
suggu create folder Logs Cache -p ./temp
suggu create file notes.txt settings.json -p ./docs
```

Created folders receive a `.gitignore`. File creation makes missing parent folders and skips existing files unless `--force` is supplied.

### Remove folders and files

```powershell
suggu remove folder Cache -p ./temp
suggu remove file notes.txt settings.json -p ./docs
```

Folder removal is recursive. Suggu refuses filesystem-root or selected-parent removal and reports that removed data cannot be recovered.

### List folder details

```powershell
suggu list folder
suggu list folder -p ./src --depth
suggu list folder --depth --max-depth 3
```

The depth view shows a tree, file/folder totals, sizes, extension summaries, and per-folder details.

### Find files or folders

```powershell
suggu find --file Controller
suggu find --folder Features -p ./src
suggu find --file "*.json" --sln-search
```

Search is recursive and case-insensitive. Partial names and `*`/`?` wildcards are supported.

## .NET solution and project commands

### Guided source creation

```powershell
suggu add class
suggu add interface
suggu add controller
suggu add json
```

When arguments are omitted, Suggu prompts for a name, shows project/layer choices with arrow keys, and asks for a folder relative to the selected project. Examples:

```text
Features/Orders/Commands
Contracts/Payments
Controllers/Admin
Configuration/Seed
```

Explicit commands:

```powershell
suggu add class CreateOrderHandler -l Application -p Features/Orders/Commands
suggu add interface OrderService -l Application -p Contracts/Orders
suggu add controller Orders -t api -l Api -p Controllers/V1
suggu add json seed-data -l Infra -p Configuration/Seed
```

Interfaces receive the conventional `I` prefix. C# namespaces come from the selected project's root namespace and folder. JSON names receive `.json` when omitted.

### Add a class library

```powershell
suggu add library
suggu add library Shop.Shared -f 10
suggu add library Shop.Web.Shared --aspnet --dry-run
```

The interactive framework selector uses the active class-library template and marks the current project's framework—or the solution's most common framework—as recommended. The new library is added to the solution.

### Add or remove references

```powershell
suggu add references --from Shop.Api --to Shop.Application
suggu add references --from Shop.Api --to Shop.Application --to Shop.Infrastructure
suggu add ref --from Shop.Api --to Shop.Infrastructure --remove
```

`--from` receives each reference. Repeat `--to` or use comma-separated targets. Self-references and duplicates are prevented; `--dry-run` is supported.

### Inspect the complete solution

```powershell
suggu project info
suggu project info --max-depth 4
```

The Spectre report includes:

- Solution path, project/layer count, source size, and file/folder totals.
- Target framework, size, and latest changed file for every project.
- Project-reference dependency trees.
- NuGet packages and versions for every project.
- Frameworks inherited from `Directory.Build.props`.
- Versions managed through `Directory.Packages.props`.
- A folder/file tree with sizes.

Generated/system folders such as `.git`, `.vs`, `bin`, `obj`, `node_modules`, and `TestResults` are excluded from this source-focused report.

### Check references and builds

```powershell
suggu check references
suggu check ref --layer Application
suggu check build
suggu check build --project ./src/Shop.Api/Shop.Api.csproj
suggu check build --no-restore
```

Reference inspection shows direct/transitive dependencies, missing targets, and cycles. Build inspection renders compiler/MSBuild diagnostics with file, line, code, and message.

### Trace an endpoint flow

```powershell
suggu check flow --controller Orders --method GetById
```

The tracer follows source-resolvable calls from a controller action through services, handlers, repositories, DTO mapping, and common MediatR dispatch. It is independent of project names and folder architecture and reports connected source files and line numbers.

This is conservative static analysis, not runtime tracing. Framework/package calls are omitted; middleware, filters, reflection, runtime DI decorators, and dynamic dispatch may add runtime steps that source inspection cannot prove.

### Locate and preview a solution file

```powershell
suggu grep -f BlogPostController
suggu grep -f blogpostcontroller.cs
suggu grep -f settings -p ./src
```

The extension is optional and matching is case-insensitive. Without `--path`, Suggu searches the enclosing solution. Multiple matches use an arrow-key selector. Readable files are previewed with line numbers; binary files return an absolute viewer path.

### List packages

```powershell
suggu list packages
suggu list packages --project Application
```

## Command reference

| Category | Command | Purpose |
| --- | --- | --- |
| General | `create folder` | Create folders and seed `.gitignore` |
| General | `create file` | Create arbitrary files |
| General | `remove folder` | Recursively remove folders |
| General | `remove file` | Remove files |
| General | `list folder` | Show folder trees, counts, types, and sizes |
| General | `find` | Recursively find files or folders |
| .NET | `create project` | Create Web API, MVC, or Console projects |
| .NET | `add class` | Add a namespaced class |
| .NET | `add interface` | Add a namespaced interface |
| .NET | `add controller` | Add an API or MVC controller |
| .NET | `add json` | Add a JSON file to a selected project |
| .NET | `add library` | Add a class library to the solution |
| .NET | `add references` / `add ref` | Add or remove project references |
| .NET | `project info` | Show frameworks, dependencies, packages, sizes, changes, and structure |
| .NET | `check references` / `check ref` | Show reference order |
| .NET | `check build` | Build and explain diagnostics |
| .NET | `check flow` | Trace connected endpoint source |
| .NET | `grep` | Locate and preview a solution file |
| .NET | `list packages` | List project packages |

Run `suggu <command> --help` for every option.

## Safety rules

1. General commands never require a .NET solution.
2. .NET commands discover `.sln`, `.slnx`, and `.csproj` context by walking upward.
3. Relative artifact paths resolve from the selected project root.
4. Existing files are skipped unless `--force` is explicit.
5. Creation commands never delete; deletion is isolated under `remove`.
6. Reference removal requires explicit `--remove`.
7. Use `--dry-run` to preview supported mutations.
8. Static flow analysis cannot guarantee runtime-only behavior.

## Repository structure

```text
suggu.Cli/    Command grammar, interactive UI, validation, and rendering
suggu.Core/   Discovery, planning, execution, generation, and inspection
suggu.Tests/  Automated Core behavior tests
docs/         Architecture, requirements, status, and session memory
```

Development validation:

```powershell
dotnet format SugguCli.slnx --verify-no-changes --no-restore
dotnet build SugguCli.slnx --no-restore
dotnet test SugguCli.slnx --no-restore
```

## Author

**Shubham Kashyap** — [@kashyap369](https://github.com/kashyap369)
