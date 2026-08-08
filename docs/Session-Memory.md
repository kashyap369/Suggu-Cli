# Session memory

## Current direction

Suggu is a global filesystem and ordinary .NET productivity CLI. Root help intentionally categorizes individual paths into **General commands** and **.NET solution/project commands**. `Requirements.md` is the active command specification.

Do not restore Clean Architecture packs, fixed layer rules, entity/repository generators, or Domain/Application/Infrastructure assumptions unless explicitly requested later.

## Resume checklist

1. Read `Requirements.md`, `Architecture.md`, and `Phases.md`.
2. Check `git status --short` and preserve unrelated changes.
3. Inspect relevant source and tests before modifying behavior.
4. Verify both command grammar/help and Core behavior.
5. Update continuity docs when requirements, behavior, limitations, verification, or next steps change.

## Baseline recorded 2026-08-08

- Target framework: .NET 10.
- Global tool package version: `0.4.0` after guided add commands and comprehensive `project info` reporting.
- Automated tests before final docs: 52 passed, 0 failed.
- Full build: 0 warnings, 0 errors.
- Release package `nupkg/suggu.0.3.0.nupkg` was built, the previous global `suggu` installation was uninstalled, and this local package was installed globally and verified with `suggu --version` on 2026-08-08.
- Release package `nupkg/suggu.0.3.1.nupkg` was then built and the global tool was updated from `0.3.0` to `0.3.1`; installed help verifies `add library`.
- Release package `nupkg/suggu.0.3.2.nupkg` was built and globally installed; installed help verifies separate `remove folder` and `remove file` commands and creation commands contain no removal option.
- Release package `nupkg/suggu.0.3.3.nupkg` was built and the global tool was updated from `0.3.2` to `0.3.3`. The installed tool was verified against `D:\Projects\Portfolio\PortfolioBackend` without modifying it: endpoint flow now reports the connected workspace source chain and omits unresolved framework/package calls.
- Release package `nupkg/suggu.0.3.4.nupkg` was built and the global tool was updated from `0.3.3` to `0.3.4`. Endpoint discovery is folder/architecture neutral for concrete and interface dependencies, ordinary field names, and partial controller files; the installed tool was regression-verified against the read-only Portfolio solution.
- Release package `nupkg/suggu.0.3.5.nupkg` was built and the global tool was updated from `0.3.4` to `0.3.5`. Installed `suggu grep -f blogpostcontroller` was verified from the Portfolio solution: it found and previewed `BlogPostController.cs` without an extension and with different casing. Full-name matching and interactive duplicate selection remain supported.
- Release package `nupkg/suggu.0.3.6.nupkg` was built and the global tool was updated from `0.3.5` to `0.3.6`. Installed `suggu create project console 10 --name SugguConsoleSmoke --no-sln --dry-run` was verified and wrote no files.
- Release package `nupkg/suggu.0.3.7.nupkg` was built and the global tool was updated from `0.3.6` to `0.3.7`. Bare `create project` adds arrow-key project-type/framework selection, validates explicit targets against the active SDK template, and offers to open successful creations through the system-associated IDE. PTY dry-run verified Console + `net9.0`; installed version/help were verified, and no project or IDE was created/opened during checks.
- Release package `nupkg/suggu.0.4.0.nupkg` was built and the global tool was updated from `0.3.7` to `0.4.0`. It adds guided class/interface/controller/JSON creation with project-relative paths, library framework recommendation/selection, and the source-focused `project info` report. Installed reporting was verified read-only against Portfolio, including inherited `net10.0` targets and central package versions; PTY/dry-run checks created no artifacts.
- Version `0.4.0` is prepared for repository publication; use Git history as the source of truth for the resulting commits and push.
- Dry-run and temporary smoke artifacts were verified absent/removed after checks.
