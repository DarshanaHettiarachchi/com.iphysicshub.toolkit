# iPhysicsHub Toolkit

Editor toolkit that syncs the latest camera tooling into a project from a central package:

- **Install/capture** the latest camera-controller source.
- **Upgrade** an old camera-controller component to a new one, **copying matching inspector
  values automatically** (class names need not match).
- **Install a wired 2D/3D toggle button** (script + icons + button) with no manual setup.

Payload lives in the package's hidden `Templates~/` folder (a `~` folder is ignored by Unity, so
it never compiles inside the package); the tool copies it into the project on demand.

Open from `Tools > iPhysicsHub > Camera Toolkit`.

## Install

It's a Git UPM package — install it either way (both end up the same; the Package Manager UI just
writes the manifest entry for you). Requires **Git installed and on PATH** (Unity shells out to it).

### Option A — Package Manager (no manual file editing)

1. **Window ▸ Package Manager**.
2. Click **+** (top-left) ▸ **Add package from git URL…**.
3. Paste (optionally pin a version with `#v1.3.2`):

   ```
   https://github.com/DarshanaHettiarachchi/com.iphysicshub.toolkit.git
   ```

4. **Add**.

### Option B — edit `manifest.json`

Add to your project's `Packages/manifest.json` under `dependencies`:

```json
"com.iphysicshub.toolkit": "https://github.com/DarshanaHettiarachchi/com.iphysicshub.toolkit.git#v1.3.2"
```

Drop `#v1.3.2` to track the default branch.

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
