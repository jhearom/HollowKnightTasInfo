# ReplayTimerMod Export

This document describes the ReplayTimerMod-compatible export path implemented in `HollowKnightTasInfo`.

## Goal

The exporter exists so TAS-authored Hollow Knight room ghosts can be consumed by the standalone `ReplayTimerMod` mod without requiring ReplayTimerMod itself to run inside the TAS environment.

Current intended setup:

* recording/export source: Hollow Knight `v1432` under libTAS
* playback/consumer target: ReplayTimerMod on Hollow Knight `1578`

The exporter targets ReplayTimerMod's existing on-disk scene JSON format and `RTM3` room payload format.

## Main Files

Core implementation:

* [Source/ReplayExport.cs](/codex/HollowKnightTasInfo/Source/ReplayExport.cs)
* [Source/ReplayExportCodec.cs](/codex/HollowKnightTasInfo/Source/ReplayExportCodec.cs)
* [Source/ReplayExportData.cs](/codex/HollowKnightTasInfo/Source/ReplayExportData.cs)

Integration points:

* [Source/TasInfo.cs](/codex/HollowKnightTasInfo/Source/TasInfo.cs)
* [Source/DiagnosticsLogger.cs](/codex/HollowKnightTasInfo/Source/DiagnosticsLogger.cs)
* [Source/ConfigManager.cs](/codex/HollowKnightTasInfo/Source/ConfigManager.cs)
* [HollowKnightTasInfo.config](/codex/HollowKnightTasInfo/HollowKnightTasInfo.config)

Reference implementation used during porting:

* [/codex/ReplayTimerMod/src/General/FrameRecorder.cs](/codex/ReplayTimerMod/src/General/FrameRecorder.cs)
* [/codex/ReplayTimerMod/src/General/ReplayShareEncoder.cs](/codex/ReplayTimerMod/src/General/ReplayShareEncoder.cs)
* [/codex/ReplayTimerMod/src/General/DataStore.cs](/codex/ReplayTimerMod/src/General/DataStore.cs)
* [/codex/ReplayTimerMod/src/General/MiniJson.cs](/codex/ReplayTimerMod/src/General/MiniJson.cs)

## Runtime Model

### Room segmentation

Room lifecycle is tracked in [ReplayExport.cs](/codex/HollowKnightTasInfo/Source/ReplayExport.cs).

Important points:

* room boundaries are tracked from `HeroController.Update`
* scene changes are observed through `GameManager.BeginSceneTransition`
* death invalidates the active room
* recording is keyed by:
  * `sceneName`
  * `entryFromScene`
  * `exitToScene`

This matches ReplayTimerMod's route model closely enough for compatibility, but it is still a compatibility implementation, not a byte-for-byte port of every RTM edge case.

### Time model

The exporter intentionally follows ReplayTimerMod-like timing semantics rather than the TAS overlay timer.

Current behavior:

* room `totalTime` is accumulated from `Time.unscaledDeltaTime`
* sample accumulation uses `Time.deltaTime`
* sample cadence is fixed at `30 Hz`
* sample collection runs from `CameraController.OnPreRender` through [TasInfo.cs](/codex/HollowKnightTasInfo/Source/TasInfo.cs)

This split exists because:

* room segmentation needs normal gameplay/update semantics
* animation state is more reliable later in the frame

There is still an open timing-refinement follow-up. See "Known follow-ups" below.

### Animation capture

Animation export is captured per sample in [ReplayExport.cs](/codex/HollowKnightTasInfo/Source/ReplayExport.cs).

Resolver order:

1. `HeroController.animCtrl`
2. `HeroAnimationController.animator`
3. generic `tk2dSprite` / `tk2dSpriteAnimator` lookup on or under the hero

Captured fields:

* `CurrentClip.name`
* `CurrentFrame`

The exporter writes ReplayTimerMod-compatible animation tables, so sprite ghosts can render instead of the diamond fallback.

## Output Format

Each `=` dump writes under:

* `./Recording/ReplayTimerMod/Dump_<timestamp>/`

Artifacts:

* `Rooms/*.rtm3.txt`
* `ReplayCollection.rtmc.txt`
* `ReplayMod/data/*.json`
* `manifest.txt`

The `ReplayMod/data/*.json` files are the direct drop-in files for ReplayTimerMod's persistent storage.

### Scene JSON

The JSON shape is:

```json
{
  "entries": [
    {
      "snapshotId": "32 lowercase hex chars",
      "capturedAtUtcTicks": 0,
      "sceneName": "White_Palace_11",
      "entryFromScene": "Abyss_05",
      "exitToScene": "White_Palace_01",
      "totalTime": 12.1701832,
      "data": "<base64 raw-deflate RTM3 payload>"
    }
  ]
}
```

Notes:

* `data` matches ReplayTimerMod's on-disk scene JSON expectation, not its clipboard/share-string wrapper
* `snapshotId` must be unique per entry
* `capturedAtUtcTicks` is metadata only

### Snapshot IDs

ReplayTimerMod normally uses `Guid.NewGuid().ToString("N")`.

That approach was copied initially, but under the libTAS/Mono runtime used here it produced the same bogus value for every entry:

* `00000000000040008000000000000000`

That caused real interoperability problems when importing many snapshots at once.

Current exporter behavior:

* generate a 32-character lowercase hex ID by hashing room metadata/content plus dump ticks/index

This is compatible because ReplayTimerMod only requires per-snapshot uniqueness and stability, not a particular GUID generation method.

## Validation State

The current implementation has been validated locally to the following degree:

* RTM-native scene JSON loads in ReplayTimerMod
* multiple exported rooms play back with moving ghosts
* animation streams are present and render as animated knight ghosts
* a full White Palace export produced structurally valid `RTM3` payloads for all rooms
* repaired duplicate-`snapshotId` dumps that failed on another install began working once IDs were made unique

## Build and Packaging Notes

Compile command used during development:

```bash
DOTNET_CLI_HOME=/tmp/dotnet_home NUGET_PACKAGES=/tmp/nuget \
dotnet build /codex/HollowKnightTasInfo/HollowKnightTasInfo.sln \
  -p:Configuration=v1432 -t:Compile -v minimal
```

Important packaging caveat:

* `-t:Compile` refreshes `obj/v1432/net35/Assembly-CSharp.TasInfo.mm.dll`
* `bin/v1432/net35/Assembly-CSharp.TasInfo.mm.dll` may remain stale
* when manually packaging, use the `obj/` patch assembly

Manual packaging in this environment works by:

1. staging the full `lib/v1432` reference set
2. copying `bin/v1432/net35` MonoMod runtime files
3. overwriting the staged patch assembly with `obj/v1432/net35/Assembly-CSharp.TasInfo.mm.dll`
4. running `MonoMod.exe` against `Assembly-CSharp.dll`
5. packaging the monomodded result plus runtime/config/lua files

## libTAS Caveat

Replay export requires disk writes to be enabled in libTAS.

If libTAS is configured to prevent writing to disk:

* dump directories may not be created reliably
* files like `manifest.txt` or scene JSON may not appear
* Unity logging may also appear misleadingly absent

This setting caused a significant false lead during development.

## Known Follow-ups

### Timing parity

The main remaining technical follow-up is timing refinement.

Known concern:

* TAS overlay time and ReplayTimerMod-compatible room time are intentionally not the same thing

The open question is whether any remaining room-time mismatch needs further adjustment for:

* room boundaries
* load-removal semantics
* first/last sample inclusion

Tracked externally:

* issue `#3` on the fork

### Edge-case validation

Still worth testing more deliberately:

* death/reset near transitions
* savestate reload before and during a room
* same-scene return routes
* very short rooms
* pause/menu-adjacent transitions

### Packaging automation

The feature is committed and usable, but packaging remains somewhat manual. If this workflow becomes routine, the next cleanup is to make release packaging consume the fresh `obj/` output deterministically.
