# iPhysicsHub Toolkit — Dev & Agent Reference

Reference for working **on** this package. End-user usage: [`README.md`](README.md).

Editor-only, four separate windows under `Tools > iPhysicsHub`:
- **Camera Updater** — install/capture the controller source; upgrade a controller component.
- **2D-3D Toggle** — install the toggle files; build a wired toggle button in the scene.
- **UI Enhancer** — install the UI Hit Area Visualizer script; add it to the scene's Canvas.
- **Project Settings Sync** — capture current project settings into a profile asset; apply curated WebGL presets or the working profile to the project.

Logic split: shared statics + window base in [`Editor/ToolkitCore.cs`](Editor/ToolkitCore.cs);
[`Editor/CameraUpdaterWindow.cs`](Editor/CameraUpdaterWindow.cs),
[`Editor/Toggle2D3DWindow.cs`](Editor/Toggle2D3DWindow.cs), and
[`Editor/ProjectSettingsSyncWindow.cs`](Editor/ProjectSettingsSyncWindow.cs).
`Templates~/` holds the installable payload (controller `.cs`, the toggle script, `Icons/`, JSON project-setting presets) — payload, not documented here.

## Docs

| Doc | Covers |
|-----|--------|
| [architecture](Documentation~/architecture.md) | files, windows, ToolkitCore, lifecycle |
| [upgrade-tool](Documentation~/upgrade-tool.md) | selection, filter, `Upgrade()`, value copy |
| [controller-source](Documentation~/controller-source.md) | install, capture, guards |
| [toggle-install](Documentation~/toggle-install.md) | toggle files + build/wire |
| [extending-and-gotchas](Documentation~/extending-and-gotchas.md) | invariants, pitfalls |
| [agent-workflow](Documentation~/agent-workflow.md) | build/verify via Unity MCP |

## Rules

- Editor-only; no runtime code or assembly.
- Generic — discover types by reflection/`TypeCache`, never name a concrete controller class.
- After editing: refresh, check console, exercise the changed path.
