# Nova dummy lifecycle cleanup (v2.2.3 local development)

This change fixes two failure paths found by the unattended audit. It does not establish that the 12-player Airship loading crash is resolved.

## Ownership and failure handling

- Track queued, partially constructed and registered CustomNetObjects separately from the public visual list.
- Bind each object to its original client, game and spawn generation.
- Retire before cleanup. Repeated or reentrant Despawn calls do not send, remove or destroy again.
- A construction/appearance completion exception notifies failure once and rolls back the allocated object. If initial spawn transmission may have reached clients, attempt one ordinary Despawn only while that same connected host session remains valid.
- Local cleanup removes only the owned PlayerControl and its exact dummy GameData entry, unregisters from its original client, deactivates and destroys the GameObject, and drops ownership/list references. It does not depend on a live connection or host status.
- Cancelled queue entries and delayed callbacks cannot recreate retired objects. Reset invalidates the generation before local cleanup, so disconnect teardown sends no cleanup packets.
- The current-client DisconnectInternal prefix calls Reset before other teardown. Old-client disconnects must not clear a replacement client's objects.
- Assignment batches recheck the session between recipients; movement and appearance updates also reject ended/retired sessions.

## Nova failure isolation

- Nova captures the original client identity. Lost connection, changed client or host loss cleans its local pool and prevents further role-state sends.
- One failed tile cancels the same Nova owner's remaining queued pool; other Nova owners remain independent.
- Growth or render-commit timeout retires the pool instead of continuing to construct the failed shot's cosmetic objects. The affected owner remains unavailable until normal lifecycle reinitialization (such as after a meeting).
- Ordinary successful shots and ordinary charge cancellation retain reusable pools. Meeting cleanup/reinitialization remains supported.
- Range 70 on Airship / 50 elsewhere, optimized dynamic pool sizing, legacy beam glyphs, beam width/time/hit rules, wall penetration, RPC layout and IDs are unchanged.

## Verification boundaries

Runtime tests use actual C# lifecycle code with synthetic Unity/Hazel substitutes. They cover 1–15 players, multiple owners, spawn/send/appearance failures, disconnect/null client/host loss/new client, duplicate cleanup, cancelled queues, mid-send teardown, selective GameData removal and meeting recovery.

The production NetId allocator and real Vanilla PC/mobile/Switch clients are not simulated by those substitutes. Local send-request counts are not physical packet/byte measurements. Extreme simultaneous Nova usage may still concentrate name/cleanup traffic; safety cancellation is tested, not server or platform disconnect immunity.

Release and ShellDevelopment builds must use `CopyToAmongUs=false`. No Steam installation, game launch, tag/Release update or commit/push is part of this change.