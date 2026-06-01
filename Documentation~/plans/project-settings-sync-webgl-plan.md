# Project Settings Sync — WebGL Preset Plan

> Refined, build-ready plan. Focus: ship curated **WebGL + Mobile** best-practice presets so new
> projects don't have to be hand-configured each time. Companion to the original
> [`project-settings-sync-plan.md`](project-settings-sync-plan.md).

## Context
Every project the user builds targets **WebGL**. Hand-configuring Player / Quality / Graphics /
Build / Lighting for WebGL (and a mobile-WebGL variant) on each new project is repetitive and
error-prone. We add a **Project Settings Sync** window to `com.iphysicshub.toolkit` that:

- **Captures** the current project's settings into a portable profile.
- **Applies** a profile to the current project (overwrite).
- **Ships curated profiles** (WebGL desktop, WebGL mobile) inside the package so a fresh project is
  one click from sane defaults; the maintainer can capture/overwrite these.

### Confirmed decisions

- **Categories:** Player, Quality, Graphics, Build, **Lighting/Environment**. **Tags & Layers excluded.**
- **No build-target switching** — all projects are already WebGL; Apply never changes the active
  platform (removes the forced-reimport hazard).
- **Lighting/Environment is per-scene** (`RenderSettings`); Apply modifies the currently open scene(s).
- Unity **2022.3.62f3** (package min `2022.3`). Target 2022.3 APIs; guard volatile ones with try/catch.

## Architecture

Reuses the toolkit's established patterns:

- **Package-shipped profiles = JSON text in `Templates~/`** (hidden folder, not asset-DB-visible),
  read/written like the existing `.cs` templates via `ToolkitCore.GetTemplatesDir()` +
  `File.ReadAllText/WriteAllText`. Serialize with `EditorJsonUtility.ToJson(profile, true)`,
  restore with `EditorJsonUtility.FromJsonOverwrite`.
- **Working profile = `ScriptableObject` `.asset`** in the user's `Assets/` (Inspector-editable).
- **Status UI** = the `ToolkitWindowBase` `_message`/`_messageType` + `DrawStatus` HelpBox only
  (the base's duplicate-*class* machinery is irrelevant here and is not used).
- **Maintainer gate** hoisted into `ToolkitCore` and shared with `CameraUpdaterWindow`.

### Data model — `ProjectSettingsProfile` (ScriptableObject)

All `UnityEngine.Object` references stored as **asset-path strings**; enums stored as **int/string**
so profiles survive across projects/versions.

```
player: PlayerSettingsData
  colorSpace(int)   // productName/companyName/bundleVersion intentionally NOT synced (per-project identity)
  defaultScreenWidth/Height, runInBackground, usePlayerLog, allowUnsafeCode
  scriptingBackend(string), apiCompatibilityLevel(string), scriptingDefines(string[])
  webgl: { memorySize, compressionFormat(string), linkerTarget(string),
           exceptionSupport(string), dataCaching(bool), decompressionFallback(bool),
           powerPreference(string) }   // PlayerSettings.WebGL.*
quality: QualitySettingsData
  levels: QualityLevelData[] { name, pixelLightCount, shadows(int), shadowResolution(int),
           shadowDistance, masterTextureLimit, anisotropicFiltering(int), antiAliasing(int),
           vSyncCount, lodBias, maximumLODLevel, realtimeReflectionProbes, skinWeights(int),
           renderPipelineAssetPath(string) }
  webglDefaultLevel: int          // QualitySettings default level for the WebGL platform group
graphics: GraphicsSettingsData
  defaultRenderPipelineAssetPath(string), transparencySortMode(int), lightsUseLinearIntensity(bool)
  tierSettings: GraphicsTierData[]   // via SerializedObject on GraphicsSettings.asset
lighting: LightingEnvironmentData   // RenderSettings — applied to open scene(s)
  skyboxMaterialPath(string), ambientMode(int), ambientSkyColor/EquatorColor/GroundColor,
  ambientIntensity, fog(bool), fogColor, fogMode(int), fogDensity, fogStart, fogEnd,
  defaultReflectionMode(int), defaultReflectionResolution(int), reflectionBounces,
  reflectionIntensity, haloStrength, flareStrength
  // sun source is a scene Light ref → skipped (not portable)
build: BuildSettingsData
  scenes: SceneEntry[] { path(string), enabled(bool) }       // EditorBuildSettings.scenes
  development, allowDebugging, connectProfiler                // EditorUserBuildSettings
  overrideMaxTextureSize(int), overrideTextureCompression(int)  // Asset Import Overrides
  // NO active build target stored/applied — always WebGL
```

### Capture / Apply per category

| Category | Capture | Apply |
|---|---|---|
| Player | `PlayerSettings` + `PlayerSettings.WebGL.*` statics | same setters |
| Quality | `SerializedObject` on `QualitySettings` (all levels) + default level | write back via `SerializedObject`; set WebGL default level |
| Graphics | `GraphicsSettings` statics + `SerializedObject` on `GraphicsSettings.asset` | same |
| Lighting | `RenderSettings.*` statics (active scene) | set `RenderSettings.*`, `EditorSceneManager.MarkSceneDirty` on each open scene |
| Build | `EditorBuildSettings.scenes`, `EditorUserBuildSettings.*` (incl. `overrideMaxTextureSize`/`overrideTextureCompression`) | same setters |

### UI Window — `Tools > iPhysicsHub > Project Settings Sync`

```
Working profile: [ObjectField ProjectSettingsProfile] [Create New]

▼ Capture from Current Project
    [ Capture Settings ]   → fills the working profile

▼ Apply to Current Project
    Source: ( ) Working profile   ( ) Package preset: [WebGL ▼]   (dropdown lists Templates~ JSON)
    ⚠️ Overwrites current project settings (Lighting applies to the open scene).
    [ Apply Settings ]

▼ Maintainer — Save preset to package        (shared ToolkitCore gate)
    Password: [****] [Enable]
    Save as: [ webgl-mobile ]  [ Save Profile to Templates~ ]
```

- Package-preset dropdown lists `*.json` in `Templates~/` (mirrors `RefreshTemplates()` in
  `CameraUpdaterWindow`, filtered to `.json`).
- The user creates the **mobile-WebGL** preset by configuring a project (lower quality levels),
  capturing, then maintainer-saving it as e.g. `webgl-mobile.json`.

## Files

| Path | Action |
|---|---|
| `Editor/ProjectSettingsProfile.cs` | New `ScriptableObject` + nested `[Serializable]` data structs (no tags/layers) |
| `Editor/ProjectSettingsSyncWindow.cs` | New `ToolkitWindowBase` window: capture / apply / preset dropdown / maintainer save |
| `Editor/ToolkitCore.cs` | Hoist maintainer password const + a small gate helper; reuse `GetTemplatesDir()` |
| `Editor/CameraUpdaterWindow.cs` | Switch its inline maintainer gate to the shared `ToolkitCore` helper (de-dupe) |
| `Templates~/webgl.json` (+ later `webgl-mobile.json`) | Shipped curated presets (JSON) |
| `package.json` | Bump `1.4.0` → `1.5.0`; broaden `description`/`keywords` to mention settings/WebGL presets |
| `CHANGELOG.md` | Add v1.5.0 entry |
| `AGENTS.md` | Add the 4th window; update the "three windows" count; note JSON profiles in `Templates~` |

## Key behaviours

### Apply (order matters)

1. Confirmation dialog: "Overwrite current project settings? Lighting applies to the open scene."
2. **Auto-backup first:** capture current settings into a timestamped backup
   `Assets/.../ProjectSettingsBackup_<timestamp>.asset` (project settings have no Undo).
3. Wrap bulk in `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()`.
4. Apply Graphics, Quality, Build, Lighting, then most Player fields.
5. Graceful degradation: missing `RenderPipelineAsset`/skybox/scene path → warning + skip (don't clear existing).
6. **Apply recompile-triggering Player fields LAST** (`scriptingBackend`, `apiCompatibilityLevel`,
   `scriptingDefines`) — they can trigger a domain reload mid-apply.
7. `AssetDatabase.SaveAssets()`; mark open scenes dirty for lighting.

### Save preset to package (maintainer)

1. Shared `ToolkitCore` password gate.
2. `EditorJsonUtility.ToJson(workingProfile, true)` → write `Templates~/<name>.json`.
3. Refresh preset dropdown. (No `.asset`/`.meta` — `Templates~` is hidden.)

## Cross-version safety

- Store primitives/strings/ints; convert enums to int/string.
- Wrap each category's apply in its own try/catch so one failing field doesn't abort the rest;
  log a per-category summary to the Console.
- Use public APIs first; fall back to `SerializedObject` with well-known property names for
  Quality/Graphics (stable on 2022.3).

## Extensibility

New category later = add a `[Serializable]` struct + `Capture<X>()`/`Apply<X>()`, call from the main
flow. Old presets deserialize with default values; `Apply<X>()` skips when data is empty/default.

## Verification

1. Compile clean — after writing scripts, `read_console` (via Unity MCP) shows no errors.
2. Window opens at `Tools > iPhysicsHub > Project Settings Sync`; "Create New" makes a profile asset.
3. **Capture** on this project → inspect the profile asset; values match current settings.
4. **Save preset** (maintainer) → `Templates~/webgl.json` written; appears in the preset dropdown.
5. **Apply** the WebGL preset to a scratch project (or after tweaking settings): confirm backup
   asset created, settings overwritten, open-scene Environment (skybox/fog/ambient) updated, build
   import overrides set, and the active platform unchanged.
6. Confirm no runtime assembly added (files live under `Editor/`); package stays editor-only.
