# Extending & gotchas

[← AGENTS.md](../AGENTS.md)

## Invariants (don't break)

1. Installed filename = class name.
2. `Undo.RegisterCompleteObjectUndo` before the field copy; keep all mutations in the undo group.
3. Abort `Upgrade()` if `go.GetComponent(newType)` exists.
4. Don't install a class that exists at another path; in-place overwrite only for `Assets/` copies.
5. Install destination must be `Assets` or under `Assets/`.
6. Rename via `RenameIdentifierInCode` (code only), never a blind file-wide replace.
7. Editor-only, generic — no runtime code, no concrete controller class named in source.
8. Retarget the toggle with the **namespace-qualified** type (`FullTypeName`) so a namespaced
   controller compiles without an added `using`.

## Pitfalls

- `GetTemplatesDir()` can be null (loose scripts, not a resolved package) — null-check.
- `MonoScript.GetClass()` can be null — null-check before `.Name`.
- `FindAssets` is fuzzy; the `GetClass().Name == className` check is what makes it correct.
- Same-named template files in different `Templates~` subfolders are ambiguous in the dropdown.
- Capture fails on git-installed (read-only) packages.
- Don't install a controller into this home repo if the class already exists here (guard fires).
