# Changelog

All notable changes to this package are documented here.

## [1.5.0]

### Added
- **Project Settings Sync** window (`Tools > iPhysicsHub > Project Settings Sync`).
  - Capture current project settings (Player, Quality, Graphics, Lighting, Build) into a portable `ProjectSettingsProfile` asset.
  - Apply a profile back to the project (or a package-shipped JSON preset) with auto-backup, confirmation dialog, and per-category error isolation.
  - Ships curated **WebGL preset** in `Templates~/webgl.json`.
  - Maintainer save: write the working profile back to `Templates~` as a JSON preset (shared password gate).
- Shared maintainer password gate moved to `ToolkitCore` and reused by both `CameraUpdaterWindow` and `ProjectSettingsSyncWindow`.

### Changed
- `CameraUpdaterWindow` now uses the shared `ToolkitCore.CheckMaintainerPassword` helper.

## [1.3.2]

### Fixed
- Toggle retarget now writes the **namespace-qualified** controller type, so selecting a
  controller that lives in a namespace produces a script that compiles (was emitting the bare
  type name with no matching `using`).
- `VersionOfName` parses with `long`/`TryParse`, so a controller name ending in a very long digit
  run no longer throws and breaks the toggle window.
- *Install toggle files* now copies the icons even when the script hits the duplicate-class guard,
  so resolving via *Update existing in place* has the icons available.

## [1.3.1]

### Fixed
- *Add toggle to scene* now builds the button with **pivot (0.5, 0.5)** (was (1,1), which shifted
  it off the reference position) and sets the **icon color to black** to match the original
  `Toggle2D3DButton`.

## [1.3.0]

### Added
- **2D/3D Toggle — Controller type dropdown**: the toggle window now lists project controllers
  (filtered, defaulting to the latest by version) and **rewrites the toggle script's controller
  type to the selected one on install**. Fixes the toggle staying bound to `CameraControllerV3`
  after a controller is upgraded (e.g. to `CameraControllerV4`). The field stays strongly typed —
  no reflection is added to the shipped runtime.

### Changed
- `ToolkitCore` now exposes the shared type-discovery helpers (`GetProjectControllerTypes`,
  `IsProjectType`, `NiceTypeName`, `VersionOfName`, `LatestIndexByName`, `GetFieldType`) and a
  text-based install primitive (`InstallScriptText`) used by both windows; the duplicate-class
  "Update existing in place" path now preserves a caller's rewritten source.

## [1.2.0]

### Changed
- Renamed the package to `com.iphysicshub.toolkit` ("iPhysicsHub Toolkit"); window is
  `Tools > iPhysicsHub > Camera Toolkit`.

### Added
- **2D/3D toggle install**: stores the toggle script + icons in `Templates~`. *Install toggle
  files* copies them in; after recompile, *Add toggle to scene* builds the Canvas/button/icon
  hierarchy and wires it (finds the scene controller, with a runtime auto-find fallback).
- `Camera2DToggleUI` gained a `FindObjectOfType` controller fallback in `Start()`.

## [1.1.0]

### Added
- **Controller Source** section: central store of the latest controller source in the package's
  hidden `Templates~/` folder.
  - **Install / Update in Project** — copies the chosen template `.cs` into an editable
    destination (default `Assets/Scripts`) and recompiles. Guards against duplicate-class
    errors: if the class already exists elsewhere in the project, it blocks the copy and
    offers **Update existing in place** instead.
  - **Save to Package** (maintainer) — writes a tested project `.cs` back into `Templates~/`,
    with an optional **Save as class** rename (rewrites the class identifier) so a renamed
    copy can coexist with the original.
- **Filter** field that narrows both the Target-GameObject list and the New-controller list
  (defaults to "Camera"); Target is now a filtered scene-object dropdown.

## [1.0.0]

### Added
- `Tools > Camera > Upgrade Controller` editor window.
- Replaces a chosen old controller component with a chosen new controller type on a target
  GameObject.
- Copies matching serialized inspector values (same field name + compatible type) via
  reflection, including UnityEngine.Object references.
- Report of copied / unmatched-old / defaulted-new fields.
- Full Undo support; operates on the currently open scene.
