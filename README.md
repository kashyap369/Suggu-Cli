# Suggu CLI

Suggu is a global .NET productivity tool for filesystem work and ordinary .NET solutions. It creates and inspects projects, source artifacts, references, builds, endpoint flows, packages, and folder structures without assuming Clean Architecture or fixed layer names.

- Current version: `0.5.2`
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
dotnet tool install --global suggu --version 0.5.2 --add-source ./nupkg
```

Update an existing installation:

```powershell
dotnet tool update --global suggu --version 0.5.2 --add-source ./nupkg
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

### Project rulebooks and custom commands

A rulebook is a project-owned Markdown file that defines repeatable custom Suggu commands. It can automate a small workflow such as creating an entity and its persistence files, or bootstrap an empty workspace with a solution, projects, packages, references, configuration files, and folders.

Rulebooks are declarative. They contain structured Suggu actions, not PowerShell, Bash, or arbitrary executable code.

#### Create a rulebook

Inside an existing solution:

```powershell
cd ./ShopBackend
suggu create rulebook
```

For an empty workspace that does not have a solution yet:

```powershell
mkdir ShopBackend
suggu create rulebook --path ./ShopBackend
cd ./ShopBackend
```

Suggu creates:

```text
ShopBackend/
└── docs/
    └── SUGGU-RULEBOOK.md
```

The generated file contains commented instructions and an editable `add entity` example. Use `--dry-run` to preview its location or `--force` to replace an existing starter:

```powershell
suggu create rulebook --path ./ShopBackend --dry-run
suggu create rulebook --path ./ShopBackend --force
```

#### Validate, discover, preview, and run commands

Run these commands from the rulebook workspace or any child directory. Suggu walks upward until it finds `docs/SUGGU-RULEBOOK.md`.

```powershell
# Validate every recipe without writing anything
suggu --rulebook --check

# List the custom commands defined by this project
suggu --rulebook --help
suggu rb list

# Show help for one custom command
suggu rb add entity --help

# Preview the complete combined plan
suggu rb add entity Book --dry-run

# Execute it
suggu rb add entity Book
```

The following entry forms are equivalent:

```powershell
suggu --rulebook add entity Book
suggu -rb add entity Book
suggu rb add entity Book
suggu rulebook add entity Book
```

Parameters can be positional or named:

```powershell
suggu rb add entity Book
suggu rb add entity --Name Book
```

#### Rulebook file structure

The machine-readable `suggu/v1` JSON must remain between the marked section and the `json` code fence. Ordinary Markdown documentation can be placed before or after it.

````markdown
# Shop project rules

<!-- Human-readable notes can go here. -->

<!-- suggu-rulebook:start -->
```json
{
  "schema": "suggu/v1",
  "projects": {},
  "commands": [],
  "templates": {}
}
```
<!-- suggu-rulebook:end -->

## Architecture decisions

More project documentation can go here.
````

The top-level properties are:

| Property | Purpose |
| --- | --- |
| `schema` | Rulebook format. Version 1 must use `suggu/v1`. |
| `projects` | Friendly aliases mapped to existing or planned project names. |
| `commands` | Custom multi-word commands, their parameters, and ordered actions. |
| `templates` | Reusable file contents represented as arrays of lines. |

#### Example: repeat an entity workflow

This command creates an entity, repository interface, and persistence configuration from one name:

```json
{
  "schema": "suggu/v1",
  "projects": {
    "domain": "Shop.Domain",
    "infrastructure": "Shop.Infrastructure"
  },
  "commands": [
    {
      "name": "add entity",
      "description": "Create an entity and its persistence files",
      "parameters": [
        { "name": "Name", "required": true, "type": "csharp-identifier" }
      ],
      "actions": [
        {
          "command": "add class",
          "project": "domain",
          "path": "Entities",
          "name": "{{Name}}"
        },
        {
          "command": "add interface",
          "project": "domain",
          "path": "Repositories",
          "name": "{{Name}}Repository"
        },
        {
          "command": "add class",
          "project": "infrastructure",
          "path": "Persistence/Configurations",
          "name": "{{Name}}Configuration",
          "template": "entity-configuration"
        }
      ]
    }
  ],
  "templates": {
    "entity-configuration": [
      "namespace {{Namespace}};",
      "",
      "public sealed class {{TypeName}}",
      "{",
      "}"
    ]
  }
}
```

Run it with:

```powershell
suggu rb add entity Book --dry-run
suggu rb add entity Book
```

The result is:

```text
Shop.Domain/
├── Entities/
│   └── Book.cs
└── Repositories/
    └── IBookRepository.cs

Shop.Infrastructure/
└── Persistence/
    └── Configurations/
        └── BookConfiguration.cs
```

Interfaces receive Suggu's normal `I` prefix, and namespaces are derived from the selected project and destination folder.

#### Example: bootstrap an empty multi-project solution

The same system can create a complete workspace. Put the `create solution` action first, followed by project creation. Later actions can reference projects declared earlier in the same recipe.

```json
{
  "schema": "suggu/v1",
  "projects": {
    "api": "Shop.API",
    "application": "Shop.Application",
    "domain": "Shop.Domain",
    "infrastructure": "Shop.Infrastructure"
  },
  "commands": [
    {
      "name": "setup architecture",
      "description": "Create the Shop solution and its base architecture",
      "actions": [
        { "command": "create solution", "name": "ShopBackend" },

        { "command": "create file", "name": "Directory.Build.props", "template": "build-props" },

        { "command": "create project", "project": "api", "type": "webapi", "framework": "10", "controllers": true },
        { "command": "create project", "project": "application", "type": "classlib", "framework": "10" },
        { "command": "create project", "project": "domain", "type": "classlib", "framework": "10" },
        { "command": "create project", "project": "infrastructure", "type": "classlib", "framework": "10" },

        { "command": "add reference", "from": "api", "to": "application" },
        { "command": "add reference", "from": "api", "to": "infrastructure" },
        { "command": "add reference", "from": "application", "to": "domain" },
        { "command": "add reference", "from": "infrastructure", "to": "application" },

        { "command": "add package", "project": "application", "name": "MediatR", "version": "12.5.0" },
        { "command": "add package", "project": "infrastructure", "name": "Microsoft.EntityFrameworkCore", "version": "10.0.10" },

        { "command": "create folder", "project": "api", "path": "Controllers/V1" },
        { "command": "create folder", "project": "application", "path": "Features/Orders" },
        { "command": "create folder", "project": "domain", "path": "Entities" },
        { "command": "create folder", "project": "infrastructure", "path": "Persistence/Configurations" }
      ]
    }
  ],
  "templates": {
    "build-props": [
      "<Project>",
      "  <PropertyGroup>",
      "    <TargetFramework>net10.0</TargetFramework>",
      "    <Nullable>enable</Nullable>",
      "    <ImplicitUsings>enable</ImplicitUsings>",
      "  </PropertyGroup>",
      "</Project>"
    ]
  }
}
```

Validate and execute:

```powershell
suggu rb --check
suggu rb setup architecture --dry-run
suggu rb setup architecture
dotnet build ShopBackend.slnx
```

Supported `create project` types are `webapi`, `mvc`, `console`, `classlib`, and `xunit`. The optional project `path` is relative to the workspace, so a test project can use `"path": "tests"`.

#### Supported actions

| Action | Important fields | Result |
| --- | --- | --- |
| `create solution` | `name` | Creates a `.slnx`/`.sln` through the installed .NET SDK. Place it before new projects. |
| `create project` | `project`, `type`, optional `name`, `framework`, `path`, `controllers` | Creates an SDK project and adds it to the solution. |
| `create folder` | optional `project`, `path` | Creates a project-relative folder, or a workspace-relative folder when `project` is omitted. |
| `create file` | optional `project`, `path`, `name`, optional `template` | Creates a file in a project or at workspace scope. |
| `add class` | `project`, `path`, `name`, optional `template` | Creates a namespaced C# class. |
| `add interface` | `project`, `path`, `name`, optional `template` | Creates a namespaced interface and adds `I` when needed. |
| `add controller` | `project`, `path`, `name`, `type` | Creates an API or MVC controller. |
| `add json` | optional `project`, `path`, `name`, optional `template` | Creates a `.json` file containing an empty object unless a template is supplied. |
| `add reference` | `from`, `to` | Adds a project-to-project reference. |
| `add package` | `project`, `name`, optional `version` | Adds a structured `PackageReference`. Omit `version` when using central package management. |

For centrally managed packages, define versions in `Directory.Packages.props` and omit `version` from each `add package` action. Suggu also removes an explicit package version emitted by an SDK template when that package is included as a versionless rulebook action.

#### Parameters, templates, and placeholders

Command parameters support:

| Field | Meaning |
| --- | --- |
| `name` | Parameter name used by `{{Name}}` and `--Name value`. |
| `required` | Whether execution must receive a value. Defaults to `true`. |
| `type` | `text` or `csharp-identifier`. Identifier validation rejects invalid C# names before execution. |

Templates are arrays of lines. Add `"template": "template-name"` to an action that produces one file.

Available placeholders are:

| Placeholder | Value |
| --- | --- |
| `{{ParameterName}}` | A positional or named custom-command argument. |
| `{{Namespace}}` | Namespace derived from the target project and folder. |
| `{{TypeName}}` | Output filename without its extension. |
| `{{ProjectName}}` | Selected project name. |
| `{{SolutionName}}` | Rulebook workspace directory name. |

Unknown placeholders and missing templates fail validation instead of producing partial output.

#### Planning, idempotency, and safety

Before changing the workspace, Suggu compiles every action into one ordered plan. This provides consistent behavior across built-in and custom commands:

- `--check` validates every recipe and writes nothing.
- `--dry-run` renders the complete plan and writes nothing.
- Existing projects, references, packages, folders, and files are skipped when already correct.
- `--force` allows file-producing actions to overwrite existing files; use it carefully.
- A failed operation stops the remaining plan and reports exactly where it failed.
- Duplicate output files in one recipe are rejected before execution.
- Workspace-level paths cannot escape the directory that owns `docs/SUGGU-RULEBOOK.md`.
- Project-level paths cannot escape their selected project.
- Self-references, invalid identifiers, unknown projects, actions, templates, and placeholders are rejected.
- Rulebooks cannot invoke arbitrary shell commands.

An idempotent recipe is safe to rerun. A successful second run should report only skipped operations and no created, overwritten, deleted, or failed operations.

#### Troubleshooting rulebooks

| Message | What to check |
| --- | --- |
| `docs/SUGGU-RULEBOOK.md was not found` | Run inside the workspace or one of its child folders, or create the rulebook first. |
| `rulebook markers are missing` | Restore both `suggu-rulebook:start` and `suggu-rulebook:end` comments. |
| `invalid rulebook JSON` | Validate commas, quotes, arrays, and braces inside the marked `json` block. |
| `project ... was not found or declared` | Correct the `projects` alias or add its `create project` action before dependent work. |
| `unknown rulebook placeholder` | Add the matching parameter or use a supported built-in placeholder. |
| `path ... escapes workspace/project` | Use a path relative to the workspace or selected project; do not use `..` to leave it. |
| Central package error `NU1008` | Add the SDK-emitted package as a versionless `add package` action and keep its version in `Directory.Packages.props`. |

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
| .NET | `create rulebook` | Create `docs/SUGGU-RULEBOOK.md` in a solution or empty workspace |
| .NET | `rulebook` / `--rulebook` / `rb` | Validate and run project-specific composite commands |
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
9. Rulebooks may compose only allowlisted Suggu operations and never execute arbitrary shell commands.

## Repository structure

```text
suggu.Cli/    Command grammar, interactive UI, validation, and rendering
suggu.Core/   Discovery, planning, execution, generation, and inspection
suggu.Core/Rulebooks/  Rulebook parsing, templates, validation, and composite planning
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
