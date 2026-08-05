# suggu

> Deterministic scaffolding and verification CLI for .NET clean-architecture projects.
> **suggu owns the deterministic 90%; the AI fills the semantic 10%.**

`suggu` is a global .NET tool that creates projects, layers, folders, and clean-architecture
scaffolds from a declarative **pack** — so your solution layout is generated the same way
every single time, instead of being hand-typed or improvised by an AI.

---

## Why

Most project setup work is mechanical: create the library, add it to the solution, wire the
project references, create `Entities/`, `Enums/`, `ValueObjects/`, drop in `BaseEntity`,
`IAggregateRoot`, `Result`, mirror a repository interface for every entity. None of that
needs judgement — it needs consistency.

`suggu` does that part deterministically. What's left (the actual business rules inside the
entity, the handler logic) is where you — or an AI assistant — should be spending attention.

---

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer

Check with:

```bash
dotnet --version
```

---

## Installation

### Option A — install from source (current)

```powershell
git clone https://github.com/kashyap369/Suggu-Cli.git
cd Suggu-Cli/suggu.Cli

# Build the NuGet package (lands in ../nupkg by default)
dotnet pack -c Release

# Install it globally
dotnet tool install --global suggu --add-source ../nupkg --no-cache
```

Verify:

```bash
suggu --version
suggu --help
```

### Updating to a newer local build

```powershell
cd Suggu-Cli/suggu.Cli
dotnet pack -c Release
dotnet tool update --global suggu --add-source ../nupkg --no-cache
```

### Uninstalling

```bash
dotnet tool uninstall --global suggu
```

---

## Troubleshooting the install

**`Command 'suggu' conflicts with an existing command from another tool`**

An older or differently-named package already owns the `suggu` shim. Find it and remove it:

```powershell
dotnet tool list --global
```

Look for the row whose **Commands** column is `suggu`, take its **Package Id**, then:

```powershell
dotnet tool uninstall --global <PackageIdFromThatRow>
dotnet tool install --global suggu --add-source ../nupkg --no-cache
```

**`Unable to find package suggu`**

`--add-source` is pointing at a folder with no `.nupkg` in it. Confirm the package exists:

```powershell
dir ..\nupkg
```

If it's empty, run `dotnet pack -c Release -o ../nupkg` and pass that same path to
`--add-source`. Use an absolute path if the relative one is ambiguous.

**`suggu` not recognised after a successful install**

The global tools folder isn't on your `PATH`. Add it:

- Windows: `%USERPROFILE%\.dotnet\tools`
- macOS / Linux: `$HOME/.dotnet/tools`

Then open a new terminal.

---

## Usage

```bash
suggu --help          # categorized command list
suggu help            # same thing
suggu -v              # version
```

### Common — files, folders, projects

| Command | What it does |
| --- | --- |
| `suggu create folder <names...>` | Create one or more folders |
| `suggu create file <names...>` | Create one or more files (any type) |
| `suggu create library <name>` | Create a class library and add it to the solution |
| `suggu create project <name>` | Create a Web API or MVC project, the way Visual Studio would |
| `suggu create reference` | Add project-to-project references |
| `suggu delete file <names...>` | Delete one or more files |
| `suggu list folder` | List folders in a directory |
| `suggu list packages` | List packages installed in a layer |

### Clean architecture — scaffolding

| Command | What it does |
| --- | --- |
| `suggu setup <layer> [section]` | Create a layer's canonical folders + seed files from the pack |
| `suggu add entity <Name>` | Domain entity under `Entities/` |
| `suggu add enum <Name>` | Domain enum under `Enums/` |
| `suggu add valueobject <Name>` | Value object under `ValueObjects/` |
| `suggu add exception <Name>` | Domain exception under `Exceptions/` |
| `suggu add repositories` | Scan `Entities/**` and generate mirrored repository interfaces |

### Inspection

| Command | What it does |
| --- | --- |
| `suggu info references` | Show which project references which, as a tree |
| `suggu find useless` | Find empty folders worth cleaning up |

---

## Example workflow

```bash
# 1. Lay down the layers
suggu create library MyApp.Domain
suggu create library MyApp.Application
suggu create library MyApp.Infrastructure
suggu create project MyApp.Api

# 2. Wire them together
suggu create reference

# 3. Seed the domain layer with base types
suggu setup Domain common
suggu setup Domain domainevents

# 4. Scaffold your model
suggu add entity Order
suggu add entity Customer
suggu add valueobject Money
suggu add enum OrderStatus

# 5. Generate a repository interface for every entity
suggu add repositories

# 6. Sanity check
suggu info references
```

---

## Packs — how the commands are defined

`suggu` is driven by a **pack** (`suggu.Core/Packs/Default/pack.json`) plus a folder of
templates. The pack declares:

- **layers** — how project names map to layers (`*.Domain`, `*.Application`, `*.Infrastructure`, `*.Api`)
- **generators** — each entry automatically becomes a `suggu add <name>` command
- **seeds** — each entry becomes a `suggu setup <layer> <section>` section

Adding a new scaffold means adding a generator entry and a template file. **No C# changes
required** — the command tree is built from the pack at startup.

The default pack is `clean-webapi`: a clean-architecture Web API with CQRS and DDD conventions.

---

## Project structure

```
Suggu-Cli/
├── suggu.Cli/                  # Spectre.Console.Cli front-end (commands, help, rendering)
│   ├── Commands/
│   │   ├── Common/             # create / delete / list
│   │   ├── CleanArchitecture/  # add / setup — built from the pack
│   │   └── Inspection/         # find / info
│   └── Infrastructure/         # categorized help provider, report renderer
├── suggu.Core/                 # the engine — no UI concerns
│   ├── Generation/             # planners, template renderer, entity scanner
│   ├── Packs/                  # pack loader, models, Default pack + templates
│   ├── Planning/               # Plan → PlanExecutor → ExecutionReport
│   ├── Inspection/             # layer, reference and environment inspectors
│   └── Workspace/              # solution locator, layer resolver
├── suggu.Tests/                # unit tests
└── docs/                       # design plan and notes
```

### Design notes

- **Plan, then execute.** Commands build a `Plan` of operations; `PlanExecutor` runs it and
  returns an `ExecutionReport`. Nothing touches disk mid-decision.
- **Core knows nothing about the console.** All rendering lives in `suggu.Cli`.
- **The pack is law.** Layout conventions live in JSON, not scattered across command classes.

---

## Building and testing

```bash
git clone https://github.com/kashyap369/Suggu-Cli.git
cd Suggu-Cli

dotnet build
dotnet test
```

---

## Author

**Shubham Kashyap** — [@kashyap369](https://github.com/kashyap369)
