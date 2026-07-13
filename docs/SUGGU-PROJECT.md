# suggu — Clean Architecture CLI for .NET

> A deterministic scaffolding and knowledge-engine CLI for the .NET ecosystem, designed to work hand-in-hand with AI coding agents (Claude Code) so the AI spends tokens on *thinking*, not on *typing boilerplate*.

**Author / Owner:** Shubham (.NET developer)
**Status:** Design / brainstorming complete — Phase 1 not yet started
**Working directory:** `D:\suggu`
**Document date:** 2026-07-09

---

## 1. Why we are building this (the problem)

When using Claude Code (or any AI coding agent) on a .NET clean-architecture project, a huge share of tokens is burned on **repetitive, deterministic, rule-based work**:

- Creating the same folder structures in every layer
- Writing near-identical repository interfaces for every entity
- Installing and wiring the same NuGet packages per layer
- Setting up the same clean-architecture skeleton (Api / Domain / Application / Infrastructure) for every new project
- Re-discovering project structure at the start of every session (ten exploration commands just to build a mental model)

This work has three properties that make it a bad fit for an LLM and a perfect fit for a CLI tool:

1. **It is deterministic** — the output is fully derivable from rules and existing files.
2. **It is repetitive** — the same shapes appear in every project and every feature.
3. **It is error-prone when hand-written** — each AI-generated file is a chance for drift (wrong namespace, inconsistent naming, missed DI registration).

**The core thesis:** *suggu owns the deterministic 90%, the AI fills the semantic 10%.* A CLI does the boilerplate in milliseconds, identically every time, and Claude Code shells out to it instead of generating the code. The payoff is not just token savings — it is **reliability and consistency**.

Integration with Claude Code is cheap: a section in the project's `CLAUDE.md` saying "for scaffolding, folders, and package setup, use `suggu` — here are the commands" is enough. Later, MCP makes the integration deeper.

---

## 2. The one architectural decision that matters from day one

suggu may later support Web API, MVC, Blazor, worker services, maybe Aspire. Therefore **"clean architecture web api" must not be hardcoded into the core**. The tool is structured internally as three separate things:

1. **Core engine** — file/folder operations, `.csproj` parsing, entity scanning, template rendering, config (`suggu.json`). Knows *nothing* about architecture styles.
2. **Knowledge packs** — pure data files describing a *profile*: `clean-webapi-net9` now; later `mvc-net9`, `modular-monolith`, `vertical-slice`. Each pack defines layers, folders, packages (per framework version and per database), templates, and validation rules.
3. **CLI layer** — thin command parsing that wires user input to engine + pack.

No plugin infrastructure needed — just keep the rules in data and the engine generic. Then "add MVC support" later means **writing a new knowledge pack, not refactoring the tool**. This is the cheapest insurance we can buy.

### Default clean-architecture template (decided 2026-07-09)

The reference for the built-in default pack is **[kashyap369/TaskFlow-CleanArchitecture-API](https://github.com/kashyap369/TaskFlow-CleanArchitecture-API)** (net10, PostgreSQL, EF Core + Dapper, MediatR CQRS, FluentValidation, JWT, domain events, UnitOfWork). Layer shape:

- **Domain** (no packages): `Common/` (BaseEntity, AuditableEntity, IAggregateRoot, Result), `Entities/<Module>/…`, `Interfaces/`, `ValueObjects/`, `Enums/`, `DomainEvents/`, `Exceptions/`, `Aggregates/`, `Specifications/`
- **Application** (MediatR, FluentValidation+DI): `Features/<Module>/<Entity>/Commands/<Action>/{Command,Handler,Validator}`, `DTOs/`, `Contracts/`, `Behavior/` (ValidationBehavior), `DomainEvents/`, `Exceptions/`, `DependencyInjection/DependencyRegistration.cs`
- **Infra** (EF Core, Npgsql+NodaTime, Dapper, JwtBearer, Identity.Core): `Persistence/{Context,Configurations,Repositories,UnitOfWork}`, `Dapper/`, `Security/`, `Email/`, `DomainEvents/Dispatchers/`, `Seeder/`, `Migrations/`, `DependencyInjection/`
- **Api** (Swashbuckle, JwtBearer, FluentValidation, EF Design): `Controllers/<Module>/`, `Middlewares/` (ExceptionHandling, RequestLogging), `Extensions/`, `Models/Responses/` (ApiResponse, ApiErrorResponse), `Filters/`, `Configurations/`, `Constants/`

Decisions taken from analyzing that repo:

1. **suggu enforces exact Entities→Interfaces mirroring** (`Entities/X/Y.cs` → `Interfaces/X/IYRepository.cs`) even though the reference repo is inconsistent (pluralized/flattened/extra-nested variants). The template is inspiration; the pack is law.
2. **Repository-per-aggregate-root, not per entity**, is the correct end state: default mode generates for every entity (v0.2), later `--aggregates-only` filters by `IAggregateRoot`/`BaseEntity` via Roslyn.
3. **Empty folders are persisted via `<Folder Include>` entries in the .csproj** (the template's own convention), not `.gitkeep`.
4. **Support both `.sln` and `.slnx`** when walking up to find the solution.
5. **Packs must include seed files** (BaseEntity, Result, ValidationBehavior, ExceptionHandlingMiddleware, ApiResponse, DependencyRegistration per layer…), not just folders + packages: pack = layers + folders + packages (per framework/db) + template files.
6. **User-defined architectures via YAML come later** but use the same pack schema as the built-in default — the default is itself written as an embedded data file, so external user packs are just "load from disk instead." DI registration lives in `DependencyInjection/DependencyRegistration.cs` per layer — the hook for Phase 2 Roslyn insertion. The reference repo even contains a file named `SystemRole .cs` (space before extension) — exactly the kind of drift `suggu doctor` should flag.

### Knowledge pack sketch

```json
{
  "net9": {
    "infrastructure": {
      "packages": {
        "sqlserver": ["Microsoft.EntityFrameworkCore.SqlServer@9.x", "Dapper@2.x"],
        "pgsql": ["Npgsql.EntityFrameworkCore.PostgreSQL@9.x", "Dapper@2.x"]
      },
      "folders": ["Persistence", "Repositories", "Migrations"]
    }
  }
}
```

`check packages`, `setup infra`, `doctor`, and `--help` all read from the same data. Adding .NET 10 support becomes a data edit, not a code change.

---

## 3. Command grammar (agreed design)

Consistent verb-noun shape, standard `--flag value` options (parse with `System.CommandLine` or `Spectre.Console.Cli`):

```
suggu new <ProjectName> --arch clean --framework 9 --db pgsql
suggu add folder <Name> --layer domain
suggu add repositories            # scan Entities/, generate interfaces
suggu add valueobjects
suggu setup domain|application|infra|api
suggu check layers                # detect + validate structure
suggu check packages [--layer domain]
suggu doctor                      # full diagnosis
suggu explain / --help            # knowledge output for humans and AI
```

### Cross-cutting design rules

- **`--json` output on every command** (possibly the default). Claude parses structured output far more cheaply and reliably than prose. E.g. `{"layer":"infrastructure","installed":[...],"missing":[...]}`.
- **Idempotency everywhere.** Running a generator twice must never clobber customized files. Skip existing by default, offer `--force`, report "created 3, skipped 5 (already exist)".
- **Detection with a fallback.** Detect layers by naming convention (`<Project>.Api`, `<Project>.Domain`, `<Project>.Infra` / `<Project>.Infrastructure`) — covers ~90% of repos — then write the result to `suggu.json` at the repo root. That file becomes the source of truth afterward and the escape hatch for non-standard layouts (`src/` folders, `Core` instead of `Domain`, etc.).
- **Walk up to find the `.sln`** rather than assuming the tool runs from solution root — an agent's working directory isn't always where you expect.

### Repository generator details (the killer feature)

- Scan `Domain/Entities/**` recursively; mirror the subfolder structure under `Domain/Interfaces/` (folder name configurable); generate `I{Entity}Repository`.
- Entity definition: start with "every top-level public class in the Entities folder"; add filters (e.g. inherits `BaseEntity`) later.
- Consider generating a generic `IRepository<T>` base once, with per-entity interfaces extending it — keeps generated files nearly empty (good: less to regenerate).
- Templates: simple string templates with placeholders to start; **Scriban** if more power is needed. File-scoped namespaces, match the user's code style.

---

## 4. Roadmap

### Phase 1 — Now: the token-saver (all deterministic)

Build order (incremental, each step useful on its own):

1. **v0.1 — foundations:** CLI skeleton, layer detection + `suggu.json`, `add folder`. Small, but forces solving project discovery, which everything depends on.
2. **v0.2 — the star:** `add repositories` (entity scan → interface generation, idempotent). Saves real tokens daily on its own.
3. **v0.3 — knowledge engine:** JSON rule files, `check packages`, `check layers`, `--json` output everywhere.
4. **v0.4 — setup commands:** `setup domain/application/infra/api` (folders + `dotnet add package` driven by the knowledge base).
5. **v0.5 — `suggu new`:** full clean-architecture project scaffolding (api / domain / application / infra layers, compatible packages per layer, per framework version 8/9/10, per database sqlserver/pgsql). Built *last* deliberately — biggest feature but least frequently run, and by then the setup/check commands provide most of its internals. Internally: `dotnet new` for projects + chained setup commands.
6. **Then:** `doctor`, value objects.

Phase 1 alone achieves the stated goal: **Claude does thinking, suggu does typing.**

### Phase 2 — Deeper into the .NET ecosystem (still deterministic, more ambitious)

- **Application-layer scaffolding** — where the *most* repetitive .NET code lives: CQRS commands/queries + handlers (MediatR style), DTOs, FluentValidation validators, AutoMapper/Mapster profiles. `suggu add feature Product --crud` generating command/handler/validator/DTO/endpoint stubs saves more tokens per invocation than anything else.
- **Implementation stubs in infra** — companion to the interface generator: `ProductRepository : IProductRepository` with EF Core boilerplate, plus DI registration (`services.AddScoped<...>()`), ideally by editing the DI extension method itself.
- **Roslyn instead of string templates** — the key upgrade of this phase. Roslyn lets suggu *read* code semantically (find entities by base class, check whether a DI registration already exists) and *modify* files safely (insert a registration without regex hacks). Takes suggu from "creates new files" to "safely evolves existing code" — prerequisite for MVC support and migrations.
- **More profiles** — MVC (controllers + views + view models), minimal API vs controller-based toggle, Blazor, worker services. Each is just a knowledge pack + templates if Phase 1 was done right.
- **Diagnostics with teeth** — `suggu doctor` enforcing architecture: "Domain references Infrastructure — violation," "Entity Product has no repository," "package version mismatch across layers." Deterministic rules; big value in teams.
- **Distribution** — ship as a global `dotnet tool` (`dotnet tool install -g suggu`) early. Native distribution channel for the ecosystem; makes CI usage trivial.

### Phase 3 — MCP server

Key insight: **an MCP server is mostly a thin wrapper around the core engine** — this is why engine/CLI separation matters. Use the C# MCP SDK (`ModelContextProtocol` package, Microsoft/Anthropic collaboration). Same codebase, two frontends: `suggu` (CLI) and `suggu mcp` (server).

Two things MCP changes:

- **Tools:** `scaffold_repository(entity)`, `check_architecture()`, `add_feature(name, options)` — typed schemas, no shell-quoting, structured results. Gain over a good `--json` CLI is modest; don't rush it.
- **Resources / context — the bigger win:** suggu feeds knowledge to Claude *proactively*. On session start, expose a resource like: "this solution has 4 layers, 12 entities, 9 have repositories, 3 packages outdated, profile clean-webapi-net9." Claude gets a perfect mental model for a few hundred tokens instead of ten exploration commands. **The knowledge engine becomes a context engine** — that is where MCP genuinely beats a CLI.

### Phase 4 — AI-driven possibilities

Guiding philosophy: **suggu owns the deterministic 90%, AI fills the semantic 10%.** Ideas in rough order of value:

1. **AI-completed templates:** `suggu add feature Order --crud` scaffolds everything deterministically but leaves marked slots (`// suggu:ai — validation rules for Order`) where business logic goes; Claude fills only the slots. This *inverts* the workflow: instead of AI writing files and hopefully following conventions, the tool writes files and AI contributes only judgment.
2. **Semantic doctor:** deterministic doctor finds structural violations; an AI layer explains *why* and proposes fixes ("Domain references EF Core — here's how to move it to Infrastructure"). Prefer outputting findings in a format Claude Code consumes over calling the API directly — cheaper, no key management.
3. **Spec-to-scaffold:** `suggu generate --from spec.md` — AI parses a plain-English feature description into a *structured plan* (JSON: entities, properties, relationships); the deterministic engine validates and executes the plan. **The AI never touches files — it only produces data.** This is the safest AI architecture and the recommended pattern for all AI features.
4. **Migration advisor:** "upgrade net8 → net9" — deterministic parts (package bumps from the knowledge base, TFM changes) + AI narration of breaking changes needing human decisions.
5. **Learning conventions:** scan an existing (brownfield) codebase and infer its conventions (naming, folder layout, base classes) into a suggu profile, so the tool adapts instead of imposing greenfield structure.

---

## 5. What we deliberately will NOT do

- **No AI features before Phase 2 is solid.** The deterministic core is the moat and the value; AI on a shaky engine just adds nondeterminism.
- **No MCP before the CLI has real users** (i.e. the author using it daily with Claude Code). CLI usage reveals which tools/resources actually matter.
- **No other languages/ecosystems.** "The clean-architecture tool for .NET" is a sharp identity; "scaffolding for everything" competes with a hundred generic generators. Ecosystem expansion means *within .NET* (MVC, Blazor, workers, Aspire) via knowledge packs.

---

## 6. Compass heading

> **Phase 1** makes Claude stop wasting tokens.
> **Phase 2** makes suggu the authority on your architecture.
> **Phase 3** makes that authority directly consumable by AI.
> **Phase 4** lets AI drive suggu instead of replacing it.

Each phase is useful on its own even if we stop there.

---

## 7. Open questions (to settle before/while coding Phase 1)

- Parsing library: `System.CommandLine` vs `Spectre.Console.Cli` (both fine; pick one and commit).
- Generated code style: file-scoped namespaces confirmed; nullable annotations, base `IRepository<T>` yes/no, folder name `Interfaces` vs `Repositories` (make configurable).
- Is `--json` the default output or a flag?
- Exact `suggu.json` schema (layer map, profile name, style options).

---

## 8. Re-onboarding prompt (copy-paste this to Claude in a fresh session)

```
I'm building "suggu" — a deterministic CLI scaffolding tool for the .NET
ecosystem, working directory D:\suggu. Read SUGGU-PROJECT.md in the repo
root first; it contains the full design we already agreed on. Summary:

WHY: When I use Claude Code on .NET clean-architecture projects, huge
token amounts are wasted on repetitive deterministic boilerplate (folder
creation, repository interfaces per entity, package setup per layer,
project skeletons). suggu does that work deterministically in
milliseconds so Claude only does the logical/semantic thinking. Thesis:
"suggu owns the deterministic 90%, AI fills the semantic 10%."

DEFAULT TEMPLATE (locked): the built-in clean-architecture pack is
modeled on github.com/kashyap369/TaskFlow-CleanArchitecture-API
(net10, PostgreSQL, EF Core + Dapper, MediatR CQRS, FluentValidation,
JWT, domain events, UnitOfWork) — full layer/folder/package breakdown
and the six decisions derived from it are in SUGGU-PROJECT.md section 2.
Key ones: enforce exact Entities→Interfaces mirroring; empty folders
persisted via <Folder Include> in csproj; support .sln and .slnx; packs
include seed files, not just folders+packages; user-defined YAML
architectures later reuse the same pack schema as the embedded default.

ARCHITECTURE (locked decision): three internal parts —
(1) core engine: file ops, .csproj parsing, entity scanning, template
    rendering, suggu.json config; knows nothing about architecture styles;
(2) knowledge packs: pure data files defining a profile
    (clean-webapi-net9 first; later mvc, blazor, workers) — layers,
    folders, packages per framework/db, templates, validation rules;
(3) thin CLI layer. This keeps future MVC/Blazor support = new data pack,
    not a refactor. Later an MCP server wraps the same engine.

COMMANDS: suggu new <Name> --arch clean --framework 9 --db pgsql |
suggu add folder <Name> --layer domain | suggu add repositories (scan
Domain/Entities/** recursively, mirror subfolders, generate
I{Entity}Repository under Domain/Interfaces) | suggu add valueobjects |
suggu setup <layer> | suggu check layers/packages | suggu doctor.
Rules: every command has --json output; everything idempotent (skip
existing, --force to overwrite); layer detection by naming convention
with suggu.json as persisted source of truth; walk up to find the .sln.

ROADMAP: Phase 1 (now): v0.1 CLI skeleton + layer detection + add folder;
v0.2 add repositories; v0.3 knowledge engine + check commands; v0.4 setup
commands; v0.5 suggu new (last, built from the others). Phase 2: CQRS
feature scaffolding (add feature X --crud), infra implementation stubs +
DI registration, adopt Roslyn for reading/modifying code, more profiles,
doctor with architecture-violation rules, ship as global dotnet tool.
Phase 3: MCP server (ModelContextProtocol C# SDK), same engine, tools +
proactive context resources ("solution map" for Claude). Phase 4: AI
features where AI only produces structured plans/fills marked slots and
the deterministic engine executes — never AI writing files directly.
NON-GOALS: no AI before Phase 2 solid, no MCP before real CLI usage, no
non-.NET ecosystems.

Check the current state of the code in D:\suggu to see which version/phase
is already implemented, then continue from there with me.
```
