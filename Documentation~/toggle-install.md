# 2D/3D toggle install

[← AGENTS.md](../AGENTS.md)

`Toggle2D3DWindow` (`Tools > iPhysicsHub > 2D-3D Toggle`). Two steps, because a freshly
installed script isn't compiled until a domain reload.

## 1. Install toggle files

A **Controller type** dropdown (+ filter, default `Camera`) lists project controllers via
`ToolkitCore.GetProjectControllerTypes(filter, ToggleClassName)` and defaults to the latest by
version (`LatestIndexByName`). Install is disabled when the list is empty.

`InstallToggleFiles()`:
- Reads the template text, then **retargets its controller type to the selected one**:
  `declared = ToolkitCore.GetFieldType(text, "cameraController")`; `chosen =
  ToolkitCore.FullTypeName(selected)` (**namespace-qualified**, so a controller in a namespace still
  resolves with no added `using`); if they differ,
  `text = ToolkitCore.RenameIdentifierInCode(text, declared, chosen)` (rewrites the field type + the
  `FindObjectOfType<…>` token; the class name and comments are untouched). This is what keeps the
  toggle following a controller upgrade (e.g. V3 → V4) — the field stays strongly typed, no runtime
  reflection.
- `ToolkitCore.InstallScriptText(text, scriptDest, null, ToggleClassName)` — same filename=class +
  duplicate-class guards as the controller; on collision the prepared (rewritten) text is carried
  in the shared collision state so **Update existing in place** writes the retargeted source.
- Copies each icon in `Templates~/Icons/` (png **+ .meta**, to keep sprite import + GUID) into
  the icons destination, skipping any already present, then `ImportAsset`.

## 2. Add toggle to scene

`AddToggleToScene()` (enabled once `ToolkitCore.FindTypeByName(ToggleClassName)` is non-null):
- Finds/creates a `Canvas` (ScreenSpaceOverlay) and an `EventSystem`.
- Builds `Toggle2D3DButton` (top-right, anchor (1,1), pivot (0.5,0.5), anchoredPos (-92,-92), 80×80,
  semi-transparent `Image`, `Button`) + child `IconImage` (fills with 10px padding, color black —
  matches the reference; runtime `Start()` only swaps the sprite, not the color).
- `Undo.AddComponent` the toggle type, then wires via `SerializedObject` (generic — no
  compile-time ref): `button`, `iconImage`, `sprite3D`/`sprite2D` (loaded from the installed
  icons), and `cameraController` = `FindObjectOfType(field type)`.
- One Undo group; sets the scene dirty; selects the button.

If no controller is in the scene, `cameraController` is left null and the runtime
`FindObjectOfType` fallback in `Camera2DToggleUI.Start()` wires it at Play.

## Coupling

`Camera2DToggleUI` references its controller type by name, so it only compiles when a compatible
controller is present. Install the controller first (Camera Updater).
