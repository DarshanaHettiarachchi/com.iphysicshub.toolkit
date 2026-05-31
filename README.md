# iPhysicsHub Toolkit

Editor toolkit that syncs the latest camera tooling into a project from a central package:

- **Install/capture** the latest camera-controller source.
- **Upgrade** an old camera-controller component to a new one, **copying matching inspector
  values automatically** (class names need not match).
- **Install a wired 2D/3D toggle button** (script + icons + button) with no manual setup.

Payload lives in the package's hidden `Templates~/` folder (a `~` folder is ignored by Unity, so
it never compiles inside the package); the tool copies it into the project on demand.

Open from `Tools > iPhysicsHub > Camera Toolkit`.

## Install (Git URL)

Add to your project's `Packages/manifest.json`:

```json
"com.iphysicshub.toolkit": "https://github.com/<user>/<repo>.git"
```

Pin a version or point at a subfolder if needed:

```json
"com.iphysicshub.toolkit": "https://github.com/<user>/<repo>.git#v1.2.0"
"com.iphysicshub.toolkit": "https://github.com/<user>/<repo>.git?path=/subfolder"
```

## Controller upgrade

1. `Tools > iPhysicsHub > Camera Toolkit`.
2. Under **Install from package › Camera controller**, pick the template and click
   **Install / Update in Project** (default `Assets/Scripts`). It compiles in.
3. Set **Target GameObject**, the **Old controller**, and the **New controller** (just installed).
4. Click **Upgrade**, review the report, press **Play**. `Ctrl+Z` undoes it.
5. Delete the old script **last** (it must exist during the upgrade so its values can be read).

## 2D/3D toggle

Under **Install from package › 2D/3D toggle button**:

1. **Install toggle files** — copies the toggle script + icons in (set destinations as needed).
2. After it compiles, **Add toggle to scene** — builds the Canvas/button/icon hierarchy and
   wires it (finds the scene controller; falls back to auto-find at Play).

## Maintainer

In **Camera controller › Maintainer**, assign a tested `.cs` and **Save to Package** (optionally
under a new class name) to write it into `Templates~/`. Then bump `package.json` version, commit,
`git push`. Capture only works on the embedded/source package, not a read-only git cache.

Full reference: `docs/packages/iphysicshub-toolkit.md`.
