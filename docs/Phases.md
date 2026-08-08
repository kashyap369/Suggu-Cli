# Implementation status

Status recorded on 2026-08-08.

## Implemented

- Root help separates General commands from .NET solution/project commands at the individual subcommand level.
- Folder creation seeds `.gitignore`; recursive deletion is isolated under `remove folder`.
- Arbitrary file creation and force overwrite are supported; deletion is isolated under `remove file`.
- Direct folder listing and `--depth` tree plus complete count/size overview are supported.
- Recursive file/folder search supports current path, explicit path, solution scope, partial names, and wildcards.
- Web API/MVC/console creation supports positional (`webapi 10`, `mvc 10`, `console 10`) and option grammar, name prompting/defaulting, SDK checks, solutions, controllers where applicable, and dry runs.
- Bare project creation provides arrow-key type/framework selection from the active SDK template, and successful creation can open the resulting solution/project in the associated IDE after confirmation or via `--open`.
- Reference trees are available as `check ref`/`check references` with optional project/layer filtering.
- References can be added or removed in batches with `add ref`/`add references`.
- API/MVC controller templates detect Web projects and default to a `Controllers` folder.
- Class/interface creation uses project and optional folder selection with interactive prompts when available.
- Class/interface/controller/JSON creation now supports missing-name wizards, arrow-key project/layer selection, and clearly explained project-relative nested paths.
- `add library` prompts for its name and active-template framework, marking the current/majority solution target as recommended.
- `project info` renders solution/project frameworks, dependency trees, packages, sizes, latest modifications, and a detailed source tree, including centrally inherited frameworks/package versions.
- Endpoint flow tracing, build diagnostics, package listing, `add library`, and file preview remain available.
- Endpoint flow output now suppresses unresolved framework/package/local noise, excludes test-project files, and resolves common Dapper row, LINQ lambda, local-variable, DTO mapper, and type-alias links.
- Endpoint flow discovery is independent of solution folder/layer conventions, supports concrete as well as interface dependencies and non-underscore fields, and locates actions across partial controller files.
- `grep --file` provides case-insensitive full-filename or extensionless stem search, optional path/solution scope, interactive duplicate selection, text preview, and binary-path fallback.
- `create rulebook` generates a documented `docs/SUGGU-RULEBOOK.md`; explicit `--rulebook`/`rb` mode validates and executes project-specific composite commands through existing safe planners.
- Rulebook v1 supports project aliases, parameters, ordered built-in actions, optional templates/placeholders, list/help/check, dry-run/force, duplicate-output detection, and project-root path containment without shell execution.
- Rulebooks can bootstrap empty workspaces with solutions, Web API/MVC/console/class-library/xUnit projects, workspace files, structured package references, project references, and project folder trees in one plan.

## Verification baseline

- Solution build: 0 warnings and 0 errors.
- Automated tests: 58 passed, 0 failed after empty-workspace bootstrap planning, structured package editing/idempotency/central-version normalization, future-project dry-run, and prior rulebook coverage.
- Installed `suggu 0.5.2` bootstrapped `D:\testGroundSuggu` from a rulebook modeled on PortfolioBackend: 8 projects, central packages, expected references/folders, and 4 smoke-test projects. The generated solution built with 0 warnings/errors, all 4 tests passed, `check build` succeeded, and a repeat recipe run made 0 changes.
- Smoke checks passed for categorized help, both project-creation grammars, multiple add-reference dry run, reference filtering, solution find, readable grep preview, folder `.gitignore`, depth overview, dedicated `remove file`, dedicated recursive `remove folder`, and class/interface dry runs.

## Future work

Add new requirements to `Requirements.md` before implementation. Keep commands architecture-neutral unless the active requirements explicitly change that direction.
