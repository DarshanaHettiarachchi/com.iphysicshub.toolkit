# Agent workflow (Unity MCP)

[← AGENTS.md](../AGENTS.md)

No automated tests — verify = compile clean + exercise the changed path live.

## Loop

1. Edit a file under `Editor/` (`ToolkitCore.cs`, `CameraUpdaterWindow.cs`, `Toggle2D3DWindow.cs`).
2. `refresh_unity` (`compile: request`, `scope: scripts`/`all`, `wait_for_ready: true`).
3. `read_console` (`types: ["error"]`) → expect 0. The MCP "Cannot access a disposed object" line
   is not a compile error.

Pin the instance with `set_active_instance` (or `unity_instance` per call) if several are connected.
After a package rename, a stale resolve can leave the assembly unloaded — call
`UnityEditor.PackageManager.Client.Resolve()` + `CompilationPipeline.RequestScriptCompilation()`.

## Exercising internals

Logic is private — drive it with `execute_code` + reflection:
- get the window type (`CameraUpdaterWindow` / `Toggle2D3DWindow`), `ScriptableObject.CreateInstance`,
  set fields / call methods with `BindingFlags.NonPublic | Instance` (or `Static` on `ToolkitCore`).
- read back `_message`, `_report`, `_collisionPath`, `_templateNames`, etc.; `DestroyImmediate` when done.
- CodeDom is C# 6: no `using` in the snippet (fully-qualify), disambiguate `UnityEngine.Object`.
- `File.Delete` is blocked by default — re-run with `safety_checks: false` only for temp cleanup.

## Cleanup

This repo is the controller's home (`Templates~` masters). Delete any temp `Assets/_*` folders and
temp `Templates~` files you create. Don't install a controller whose class already exists here.
