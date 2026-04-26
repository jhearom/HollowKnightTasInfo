# Desyncs in HollowKnightTasInfo

This document explains what "desync" usually means in the Hollow Knight libTAS workflow, what is known versus inferred about the cause, and which `HollowKnightTasInfo` features exist to work with desync-prone scenarios.

For the current `1432` transition investigation notes, see [desync-investigation-1432.md](/codex/HollowKnightTasInfo/docs/desync-investigation-1432.md).

## What "desync" means here

In this repo, a desync is not primarily an overlay bug or a cosmetic mismatch. It means that the same movie, savestate, or playback setup no longer reproduces the same in-game behavior at the same frames.

Typical symptoms:

* control returns a frame or two earlier or later after a transition
* the knight accepts inputs on different frames than before
* movement or state progression diverges even though the input movie is unchanged
* RNG calls happen at different times or in different counts, causing further divergence
* later scenes no longer match the run that originally produced the movie

In practice, this is a determinism failure: replaying to a later point from the same authored input does not recreate the same game state.

## Where desyncs commonly show up

Based on the current tooling and README guidance, desyncs are most noticeable when:

* replaying a long movie from the start
* using fast-forward aggressively
* making or loading savestates near scene transitions
* encoding a movie to final video
* splicing scene-level inputs together
* trying to keep multiple TASes synchronized

The user-visible failure often appears around transitions, but the actual cause may have happened slightly earlier.

## What is probably causing them

This repo does not claim a single fully-proven root cause. The practical model, based on the features implemented here, is:

* Hollow Knight under libTAS is sensitive to timing around scene loads and transitions
* patch `1432` is especially desync-prone
* RNG divergence is a major secondary effect and often becomes the most obvious long-term consequence
* fast-forward during loads can make sync stability worse

It is plausible that this is related to how libTAS advances Unity through timing-sensitive periods, potentially including thread scheduling or load behavior. This document treats that as an informed hypothesis, not a proven low-level diagnosis.

## Existing repo features for working with desyncs

### 1. Fast-forward protection during loads

Config key:

* `DisableFFDuringLoads`

Purpose:

* reduces one known source of instability by marking load phases as unsafe for fast-forward

How it works:

* the tooling watches game loading state
* when a load begins or ends, it updates `TasInfoFlags`
* the newer libTAS Lua script can then disable fast-forward while the load is in progress

Relevant code:

* [README.md](/codex/HollowKnightTasInfo/README.md)
* [Source/BaseTimer.cs](/codex/HollowKnightTasInfo/Source/BaseTimer.cs)
* [Source/TasInfoFlags.cs](/codex/HollowKnightTasInfo/Source/TasInfoFlags.cs)
* [HollowKnightTasInfo_v2.lua](/codex/HollowKnightTasInfo/HollowKnightTasInfo_v2.lua)

What it helps with:

* replay stability around loads, especially on `1432`

What it does not do:

* guarantee perfect determinism
* fix RNG divergence that already happened
* eliminate savestate edge cases near transitions

### 2. RNG synchronization and playback

Purpose:

* makes scene-level splicing and replay recovery more practical in a game with unstable RNG

How it works:

* RNG calls are recorded to `./Recording/RNG`
* if matching playback files exist in `./Playback/RNG`, the tooling feeds recorded RNG values back into the game
* when playing back a recorded value, the code still advances Unity's RNG once underneath, so seed progression stays as aligned as possible

Relevant code:

* [README.md](/codex/HollowKnightTasInfo/README.md)
* [Source/RandomInjection.cs](/codex/HollowKnightTasInfo/Source/RandomInjection.cs)
* [Source/PlaybackSystem.cs](/codex/HollowKnightTasInfo/Source/PlaybackSystem.cs)

What it helps with:

* scene-by-scene splicing
* recovering from RNG-driven divergence
* keeping reproduced scenes closer to the original authored run

What it does not do:

* make a fundamentally different game state behave like the original
* solve non-RNG timing divergence by itself

### 3. MultiSync

Config keys:

* `RecordMultiSync`
* `MultiSyncName`
* `MultiSyncConsolidateGeo`

Purpose:

* synchronizes state across multiple TASes or across separately-authored segments

How it works:

* the tooling records game-state changes such as geo, items, player data, and other world progression
* playback reads `MultiSync*.txt` files from `./Playback`
* only entries whose timestamps are still in the future are applied during playback

Relevant code:

* [README.md](/codex/HollowKnightTasInfo/README.md)
* [Source/MultiSync.cs](/codex/HollowKnightTasInfo/Source/MultiSync.cs)
* [Source/PlaybackSystem.cs](/codex/HollowKnightTasInfo/Source/PlaybackSystem.cs)

What it helps with:

* coordinating multiple simultaneous TASes
* bringing world/player progression back into alignment when working from recorded state

What it does not do:

* replace deterministic replay
* fix every timing-sensitive gameplay divergence

### 4. Diagnostics and desync detection

Purpose:

* helps identify when the tooling itself may be perturbing state
* provides logs useful for understanding where divergence began

Relevant pieces:

* `Diagnostics` and `Inputs` exports in the README
* [Source/DesyncChecker.cs](/codex/HollowKnightTasInfo/Source/DesyncChecker.cs)
* [Source/RngInfo.cs](/codex/HollowKnightTasInfo/Source/RngInfo.cs)
* [Source/DiagnosticsLogger.cs](/codex/HollowKnightTasInfo/Source/DiagnosticsLogger.cs)

Notable caveat:

* `DesyncChecker` is a lightweight RNG-state detector, not a recovery system
* its user-facing reporting is currently commented out because some tooling features legitimately touch RNG-related state

## A practical mental model

For normal TAS work, it is reasonable to think of desyncs this way:

1. A timing-sensitive moment differs from the original authored run, often near a load or transition.
2. Control returns on a different frame, or game state settles differently.
3. The input movie is now acting on the wrong frame/state.
4. RNG and later gameplay drift further away from the original run.

That is why the current mitigations focus on:

* protecting loads from fast-forward
* recording and replaying RNG
* replaying recorded state where needed

## What this repo does not currently promise

The current tooling does not promise:

* fully deterministic reproduction across every patch and workflow
* a complete root-cause explanation for every desync
* automatic recovery from arbitrary divergence
* immunity to savestate problems near transitions

Instead, it provides targeted tools that reduce common failure modes and make desync-prone workflows more manageable.
