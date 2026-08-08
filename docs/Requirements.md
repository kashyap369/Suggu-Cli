# Suggu requirements

## Product purpose

Suggu is a globally installed productivity CLI for filesystem work and ordinary .NET solutions, especially ASP.NET Core Web API and MVC development. It must not assume Clean Architecture or fixed Domain/Application/Infrastructure project names.

Root help must separate every command into exactly two clear groups: **General commands** and **.NET solution/project commands**.

## General commands

### Create folders

```text
suggu create folder <foldername...> [-p|--path <PATH>]
```

- Work from any directory.
- Use the current directory when `--path` is omitted.
- Support nested folder names and multiple names.
- Every created target folder must contain a `.gitignore` file.

### Create files

```text
suggu create file <filename...> [-p|--path <PATH>] [-f|--force]
```

- Work with arbitrary extensions from any directory.
- Use the current directory when `--path` is omitted.
- Create missing parent folders.
- Skip existing files unless `--force` is used.

### Remove folders or files

```text
suggu remove folder <foldername...> [-p|--path <PATH>]
suggu remove file <filename...> [-p|--path <PATH>]
```

- Removal is a separate, explicit command and is never mixed into `create`.
- Use the current directory when `--path` is omitted.
- `remove folder` recursively removes each named folder and all its contents.
- `remove file` removes one or more named files.
- Skip missing targets and direct users to the correct file/folder command when the target type differs.
- Refuse to remove the selected parent directory or filesystem root.
- Clearly report that Suggu cannot recover removed data.

### List folders

```text
suggu list folder [-p|--path|--find-in <PATH>] [-d|--depth] [--max-depth <NUMBER>]
```

- Default view lists only direct child folders of the execution/selected directory.
- `--depth` displays a recursive folder/file tree followed by a detailed overview.
- The detailed view includes file sizes, total folder/file counts, total bytes, extension counts/sizes, and per-folder direct file count, subfolder count, and recursive size.
- `--max-depth` limits tree rendering without changing the complete overview totals.

### Find a file or folder

```text
suggu find --file <NAME> [--path <PATH>] [--sln-search]
suggu find --folder <NAME> [--path <PATH>] [--sln-search]
```

- Exactly one of `--file` and `--folder` is required.
- Search recursively under the current directory when no path or solution option is given.
- `--path` supplies an explicit search root.
- `--sln-search` uses the enclosing solution root when `--path` is omitted.
- Case-insensitive partial names and `*`/`?` wildcard patterns are supported.

## .NET solution/project commands

### Create an SDK project

```text
suggu create project webapi 10 [-n|--name <NAME>] [-p|--path <PATH>]
suggu create project mvc 10 [-n|--name <NAME>] [-p|--path <PATH>]
suggu create project console 10 [-n|--name <NAME>] [-p|--path <PATH>]
suggu create project --type <webapi|mvc|console> --framework <8|9|10> [--name <NAME>] [--path <PATH>]
```

- Use official installed `dotnet new webapi`, `dotnet new mvc`, and `dotnet new console` templates.
- Bare `suggu create project` in an interactive terminal must provide arrow-key project-type and target-framework choices and prompt for the project name.
- Target-framework choices must come from the active installed SDK template so only values accepted by `dotnet new` are offered. Explicit unavailable targets must produce a clear error listing available values.
- The path is the parent folder and defaults to the current directory.
- Ask for a missing name in an interactive terminal. In redirected/non-interactive use, derive `<folder>.Api`, `<folder>.Web`, or `<folder>.Console` and report it.
- Validate SDK support before execution.
- Add to an enclosing solution when present; otherwise create a solution unless `--no-sln` is used.
- Support `--controllers` for controller-based Web API templates and ignore it for MVC/console templates; support `--dry-run` for all types.
- After successful interactive creation, ask whether to open the solution/project using the operating system's associated IDE. `--open` requests the same behavior without prompting; dry runs never launch an IDE.

### Check reference order

```text
suggu check references [-l|--layer <PROJECT>]
suggu check ref [-l|--layer <PROJECT>]
```

- Without a layer/project name, render every project and its transitive references as a tree.
- With a short or full project name, render only that project's reference chain.
- Clearly show missing workspace targets and circular references.

### Add or remove references

```text
suggu add references --from <PROJECT> --to <PROJECT> [--to <PROJECT> ...] [--remove]
suggu add ref --from <PROJECT> --to <PROJECT> [--remove]
```

- `--from` is the project receiving references; each `--to` is a referenced project.
- Accept repeated `--to` options or comma-separated names.
- Resolve short/full project names, prevent self-reference, skip duplicates, support `--dry-run`, and remove existing references with `--remove`.

### Add a controller

```text
suggu add controller [NAME] [-t|--type <api|mvc>] [-l|--layer <PROJECT>] [-p|--path <PROJECT_RELATIVE_PATH>]
```

- Require an ASP.NET Core Web project.
- Interactive use prompts for a missing controller name/type and offers arrow-key project/layer selection.
- Add the `Controller` suffix when missing.
- If no path is provided interactively, prompt for a project-relative folder with `Controllers` as the default and show a nested-path example.
- API templates use `ControllerBase`, `[ApiController]`, and `api/[controller]` routing.
- MVC templates use `Controller` and a basic `Index` action.
- Derive the namespace from project root namespace and folder path; skip existing files unless `--force` is used.

### Add a class library

```text
suggu add library [NAME] [-f|--framework <VERSION>] [-p|--path <PATH>] [--aspnet]
```

- Create an SDK class-library project and add it to the enclosing solution.
- Interactive use prompts for a missing name and provides arrow-key target-framework selection from the active class-library template.
- Recommend and preselect the current project's target framework, or the solution's most common target when run from the solution root.
- Use the solution root when no path is provided.
- Optionally add the ASP.NET Core shared-framework reference.
- Support `--dry-run` and idempotent project/solution operations.

### Add a class or interface

```text
suggu add class [NAME] [-l|--layer <PROJECT>] [-p|--path <PROJECT_RELATIVE_PATH>]
suggu add interface [NAME] [-l|--layer <PROJECT>] [-p|--path <PROJECT_RELATIVE_PATH>]
```

- Interactive terminals prompt for a missing name, provide arrow-key project/layer selection, and then ask for a path relative to that project, with an example such as `Features/Orders/Commands`.
- Relative `--path` values are resolved from the selected project root, so drive-qualified absolute paths are unnecessary.
- An empty folder answer requires confirmation and creates in the project root.
- Non-interactive execution requires an unambiguous current/selected project and reports project-root fallback.
- Generate the namespace from the selected project and folder.
- Prefix interface names with `I` unless they already use the conventional `I` + uppercase prefix.
- Preserve existing files unless `--force` is used and support `--dry-run`.

### Add a JSON file

```text
suggu add json [NAME] [-l|--layer <PROJECT>] [-p|--path <PROJECT_RELATIVE_PATH>]
```

- Work only in a selected .NET project and use the same guided project-relative folder flow as class/interface creation.
- Prompt for a missing name, add `.json` when omitted, and create a basic empty JSON object.
- Preserve existing files unless `--force` is used and support `--dry-run`.

### Show solution project information

```text
suggu project info [--max-depth <NUMBER>]
```

- Show the solution path/root, total project/layer count, source-focused size, folder/file totals, and the most recently modified file/date.
- List every project/layer with its target framework(s), source size, file count, and latest modified file/date.
- Render the complete project-reference dependency graph, including circular and outside-workspace references.
- List installed NuGet packages and versions separately for every project/layer.
- Resolve target frameworks inherited from `Directory.Build.props` and centrally managed package versions from `Directory.Packages.props`.
- Render the solution's folder/file structure as a detailed tree with sizes; `--max-depth` limits tree display without changing complete totals.
- Exclude generated/system folders (`bin`, `obj`, `.git`, `.vs`, `node_modules`, and `TestResults`) from the source-focused report.

### Trace endpoint flow

```text
suggu check flow --controller <NAME> --method <NAME>
```

- Locate the controller action and trace statically resolvable calls through services, handlers, repositories, and common MediatR dispatch.
- Discover connected source recursively without assuming project names, architectural layers, or folder locations.
- Show file paths, lines, execution order, and confirmed/ambiguous/unresolved status.
- Show only calls that resolve to runtime workspace source files; omit framework/package calls and test-project source from the connected-file tree.
- Infer interface and concrete constructor/action dependencies, fields with or without underscore naming, common local variables, generic Dapper query rows, LINQ lambda parameters, and C# type aliases so connected source remains visible.
- Locate actions across partial controller source files and report genuinely ambiguous duplicate controller/action matches instead of silently selecting one.
- Clearly state limitations caused by runtime DI, reflection, middleware, filters, or dynamic dispatch.

### Locate and preview a solution file

```text
suggu grep -f|--file <FILENAME_OR_STEM> [-p|--path <PATH>]
```

- Search the enclosing solution when path is omitted; use the explicitly provided directory otherwise.
- Match case-insensitively. When an extension is supplied, match the exact full filename; otherwise match the exact filename stem across extensions.
- If multiple files match, interactive terminals show arrow-key selection; non-interactive use lists every match.
- Preview readable source/text formats with line numbers.
- For PDF, Word, and other unsupported binary formats, identify the format and print a copyable absolute path for an external viewer.

## Shared behavior

- Support `.sln` and `.slnx` discovery by walking upward.
- Validate command context before mutation.
- Use idempotent skip behavior where applicable.
- Preserve user content unless force/removal was explicitly requested.
- Provide readable help, success, skip, warning, and error output.
