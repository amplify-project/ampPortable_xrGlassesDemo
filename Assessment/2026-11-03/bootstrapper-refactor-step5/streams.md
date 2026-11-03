# Batch 1: Redis streams

Extracted pump acquisition/reference counting/release, sensor-channel definitions,
and engagement backend/routing into `Presentation/Bootstrap/RedisStreamRegistry`.
Bootstrapper retains serialized configuration, selects channels/modes, constructs
the registry, supplies configuration and coordinates ordered cleanup. The helper
uses existing Infra services and creates its Unity objects beneath the supplied
owner transform. It neither references Bootstrapper nor adds scene components.

Existing tests retain their assertions and target relocated state/channel parsing.
Four new tests verify final-consumer release, case-sensitive raw channel identity,
endpoint reconfiguration on the same shared pump, and engagement backend reuse/
replacement/cleanup. Transports remain inactive during these tests.

Validation: **203/203 full PlayMode tests passed**, Unity exit 0. No tests were
removed. Inspector fields/defaults/migration attributes are unchanged. The existing
user scene-object rename is preserved and excluded from this commit. Packages,
scene/prefab assets and visualization components are not edited by this batch.
