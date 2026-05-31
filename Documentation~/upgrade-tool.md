# Upgrade tool

[← AGENTS.md](../AGENTS.md)

Swaps an old controller component for a new type, copying matching fields. Works for any
`MonoBehaviour` types; names need not match.

## Filter (`_newFilter`, default "Camera")

Narrows both lists, case-insensitive:
- Target list — `MatchesFilter`: keep a GameObject if any **Component** type name contains the
  filter (so a plain `Camera` matches too).
- New-type list — `RefreshNewTypes`: keep types whose `Name` contains the filter.

Empty = show all.

## Selection

- Target — `RefreshTargets`: walk loaded scenes (incl. inactive), filter, keep an explicit
  `_target` even if unmatched, sort by `GetHierarchyPath`, auto-pick first.
- Old — `RefreshOldCandidates`: `GetComponents<MonoBehaviour>()` on target (null = missing script).
- New — `RefreshNewTypes`: `TypeCache.GetTypesDerivedFrom<MonoBehaviour>()` minus abstract/generic
  and engine/editor assemblies (`IsProjectType`), labelled by `NiceTypeName`.

`CanUpgrade` gates the button (target set, old candidate exists, new exists, old not missing,
new ≠ old).

## Upgrade()

1. Abort if `go.GetComponent(newType) != null` (no duplicate).
2. Undo group → `Undo.AddComponent`.
3. `Undo.RegisterCompleteObjectUndo(newComp)` **before** copy (so redo keeps values).
4. `CopySerializedFields` → `Undo.DestroyObjectImmediate(oldComp)` → dirty → `CollapseUndoOperations`.
5. `RefreshOldCandidates` + `BuildReport`.

## CopySerializedFields

Per `newType` field passing `IsSerializedField` (public & not `[NonSerialized]`, or non-public &
`[SerializeField]`; skips static/const/readonly): copy from a same-named source field whose type
is assignable. Buckets: copied / unmatched-old / defaulted-new → `BuildReport` (`Warning` if any
old field unmatched).
