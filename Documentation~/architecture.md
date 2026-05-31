# Architecture

[← AGENTS.md](../AGENTS.md)

## Files

```text
Editor/ToolkitCore.cs            shared statics (install, templates, helpers) + ToolkitWindowBase
Editor/CameraUpdaterWindow.cs    controller install/capture + upgrade
Editor/Toggle2D3DWindow.cs       2D/3D toggle install + build/wire
Editor/UIEnhancerWindow.cs       UI Hit Area Visualizer install + add to Canvas
Editor/...Editor.asmdef          Editor-only; references UnityEngine.UI (toggle build)
Templates~/                      installable payload: controller .cs, Camera2DToggleUI.cs, UIHitAreaVisualizer.cs, Icons/
Documentation~/                  these docs (hidden from Unity)
package.json / README.md / CHANGELOG.md
```

## Windows (two separate tools)

- **`CameraUpdaterWindow`** — `Tools > iPhysicsHub > Camera Updater`. Installs/captures the
  controller source and upgrades a controller component. Its template list excludes
  `ToolkitCore.ToggleScriptName` and `ToolkitCore.VisualizerScriptName`.
- **`Toggle2D3DWindow`** — `Tools > iPhysicsHub > 2D-3D Toggle`. Installs the toggle files, then
  builds a wired button in the scene.
- **`UIEnhancerWindow`** — `Tools > iPhysicsHub > UI Enhancer`. Installs the UI Hit Area
  Visualizer script, then adds it to the scene's Canvas.

Both derive from `ToolkitWindowBase` (shared status line + duplicate-class "update in place"
button) and call `ToolkitCore` for install/template/helper logic. UI metrics: `ToolkitCore.ButtonH`,
`ToolkitCore.LabelW`.

## ToolkitCore

**Install** — `InstallScriptText(srcText, destFolder, forceDestRel, classNameFallback)` is the core
(writes C# into the project, filename = class, Assets-scope + duplicate-class guards, `ImportAsset`);
`InstallScriptFile(srcAbs, …)` just reads a file and delegates to it. Returns `InstallResult`
(success path / message / `CollisionPath` + **`CollisionText`** — the prepared source the in-place
update writes, so a caller's rewrite, e.g. a retargeted toggle, is preserved).

**Templates / source** — `GetTemplatesDir`, `ExtractClassName`, `FindExistingScriptPath`,
`PathsEqual`, `IsValidIdentifier`, `RenameIdentifierInCode` (whole-word, code-only), `FindTypeByName`,
toggle name constants.

**Type discovery (shared by both windows)** — `GetProjectControllerTypes(filter, excludeName)`,
`IsProjectType`, `NiceTypeName`, `FullTypeName` (namespace-qualified, used to retarget the toggle),
`GetFieldType`, and `VersionOfName` / `LatestIndexByName` (trailing-digit "latest" pick).

## Lifecycle (CameraUpdaterWindow)

- `OnEnable()` — seed `_target` from selection, then `RefreshTargets/NewTypes/OldCandidates/Templates`.
- `OnSelectionChange()` — follow hierarchy selection → `_target`, re-refresh, repaint.
- `OnGUI()` — reads cached lists each frame; change-checks on Filter / target / template popups
  trigger the matching `Refresh*`.
