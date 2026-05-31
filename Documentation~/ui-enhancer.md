# UI Enhancer

[← AGENTS.md](../AGENTS.md)

`UIEnhancerWindow` (`Tools > iPhysicsHub > UI Enhancer`). Two steps, because a freshly
installed script isn't compiled until a domain reload.

## 1. Install UI Hit Area Visualizer

Reads `Templates~/UIHitAreaVisualizer.cs` and installs it into the project via
`ToolkitCore.InstallScriptFile`. Uses the same duplicate-class guards and
"Update existing in place" resolution as the other toolkit windows.

## 2. Add to Canvas

Enabled once `ToolkitCore.FindTypeByName(ToolkitCore.VisualizerClassName)` is non-null:
- Finds the first `Canvas` in the scene, or creates one (`ScreenSpaceOverlay`,
  `CanvasScaler`, `GraphicRaycaster`) if none exists.
- Uses `Undo.AddComponent` to attach `UIHitAreaVisualizer` to the Canvas GameObject.
- Marks the scene dirty and selects the Canvas.

Press **Play**, then press **F12** to toggle magenta debug overlays on all
raycast-target UI elements under that Canvas.
