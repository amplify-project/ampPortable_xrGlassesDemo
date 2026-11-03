# Sine retirement: first step 4 chunk

The user retired the original local sine-wave demo source. Removed its script and
matching metadata, Bootstrapper field, enum member, creation, enable/disable logic,
and both session subscriptions. Redis retains serialized enum value `1` and is now
the default. The main scene loses only its obsolete null `SineSource` field; all
other saved configuration and object references remain unchanged.

The existing manual graph test no longer constructs or checks the retired source;
its manual graph and disabled Redis assertions remain. The root README reflects
the supported sources. Shared session, mapping, loopback, factory, and anchoring
infrastructure is retained for the optional primary Redis path.

Validation: **13/13 existing Bootstrapper PlayMode tests passed**, Unity exit `0`.
See `sine-removed.xml`, its run record, and the byte-verified isolated snapshot
record. The deleted script GUID had no saved asset references. Lifecycle changes
and focused lifecycle regressions are the next chunk within step 4.
