# Controller source (install & capture)

[← AGENTS.md](../AGENTS.md)

Keeps the latest controller `.cs` in `Templates~` and moves it in/out of projects.
`Templates~` ends in `~` so Unity ignores it (never compiles, no collisions). Resolved via
`PackageInfo.FindForAssembly(...).resolvedPath + "/Templates~"` (`GetTemplatesDir`).

## Install — `InstallTemplate(forceDestRel = null)`

- Class via `ExtractClassName`; **dest filename = class name** (MonoBehaviour requires file==class).
- Dest must be `Assets` or under `Assets/`.
- Guard — `FindExistingScriptPath(className)`:
  - exists under `Assets/`, different path → set `_collisionPath`, offer **Update existing in place**.
  - exists in a package → warn only (no overwrite).
  - none / same path → write.
- Write → `ImportAsset(ForceUpdate)` + `Refresh`.

## Capture — `CaptureToPackage()`

- Needs `_captureScript` + resolvable `Templates~`. Class from `MonoScript.GetClass().Name`.
- Optional rename (`_captureClassName`, validated by `IsValidIdentifier`) via
  `RenameIdentifierInCode` → writes `Templates~/<class>.cs`.
- Fails on read-only (git-cache) packages — edit the embedded/source package.

## Helpers

- `RenameIdentifierInCode` — whole-word rename in **code only**; skips comments and string/char
  literals (incl. `@"…"`).
- `ExtractClassName`, `FindExistingScriptPath` (confirmed by `GetClass().Name`), `PathsEqual`,
  `IsValidIdentifier`.
