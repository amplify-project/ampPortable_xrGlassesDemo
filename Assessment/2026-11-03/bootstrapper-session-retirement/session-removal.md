# Old session removal — first chunk

Removed the explicitly retired primary scalar Redis session, spatial-anchor
registry/service/contracts, session messages/options/controller, loopback transport,
simple visualizer/mapper/factory, unused pose/spawn contracts, coroutine task adapter,
and their exclusive demo prefab. Thirteen runtime scripts and matching metadata,
the exclusive prefab and metadata, and the empty Anchors folder metadata are removed.
The active physiological/engagement pumps, shared graph contracts, generic data/
clock/mapper/visualizer interfaces, current views, and manual tools remain.

Bootstrapper no longer constructs the old session, auto-creates a fallback sphere,
looks up a primary Redis source, or configures session topics or source selection.
Its active Redis gating, discovery, automatic/manual modes, shared pumps and ordered
view cleanup remain. The saved scene loses only the obsolete session fields in this
chunk; placement and Inspector renames are the next chunk.

Validation: **16/16 Bootstrapper tests** and **192/192 full PlayMode tests** passed.
The previous suite had 210 tests: 13 tests solely for retired anchor/network contracts
and five tests solely for the retired primary session are removed. All 192 other
test identities remain. Three automatic/manual lifecycle cases are retained.

`retired-assets.json` records removed script GUIDs and their saved asset references.
The only referenced removed scripts belonged to the deleted SimpleVisualizerPrefab;
that prefab itself had no saved asset callers. Source snapshots were byte-verified
in the isolated Unity project. No packages, helper extraction, push or device action.
