# `1432` Desync Investigation Notes

This document captures the current working understanding of the `1432` transition desync investigation.

It is not a final root-cause report. It is a snapshot of what is currently grounded in code, trace data, and repeated operator-side experiments.

## Current question

Why do some `1432` transitions desync under libTAS, especially when fast-forward is used, and why does the first visible slip show up before control is returned to the Knight?

## Current scope

This note is specifically about:

* Hollow Knight patch `1432`
* libTAS on Linux
* transition desyncs, especially vertical transitions
* the issue-`#6` trace work in `HollowKnightTasInfo`

It is not intended as a general explanation for every desync on every patch.

## What is grounded so far

### 1. The first visible slip is upstream of control return

The first visible mismatch in traced good/bad samples does not start at:

* `HeroController.AcceptInput`
* `HeroController.EnterScene`
* `GameManager.OnFinishedSceneTransition`

It starts earlier, at the destination-scene load/activation boundary.

With the richer issue-6 trace, the first bad/good difference has been narrowed to the span between:

* `SceneLoad.WillActivate`
* `SceneLoad.ActivationComplete`

In the bad path, `SceneLoad.ActivationComplete` lands one render frame later than in the good path.

### 2. Hollow Knight uses its own managed scene-load wrapper

`1432` does not simply call `SceneManager.LoadSceneAsync(...)` and wait passively.

`GameManager.BeginSceneTransitionRoutine(...)` creates a `SceneLoad` object that:

* starts additive load with `LoadSceneAsync(targetScene, Additive)`
* sets `allowSceneActivation = false`
* waits for HK-owned fetch/activation gating
* calls `WillActivate`
* enables scene activation
* yields the async operation
* then runs `ActivationComplete`

This means the critical transition window is not just "Unity scene load happened". It is:

* HK gate release
* Unity additive scene becoming present
* HK `WillActivate`
* Unity activation completion
* HK `ActivationComplete`

### 3. The risky operator-visible window is extremely narrow

For the nasty first vertical repro transition currently in use:

* `3544`: `SceneLoad.SetFetchAllowed` and `SceneLoad.SetActivationAllowed`
* `3545`: first render where `unitySceneCount = 2`
* `3546`: `SceneLoad.FetchComplete` and `SceneLoad.WillActivate`
* `3550` or `3551`: `SceneLoad.ActivationComplete` and `Unity.SceneLoaded`

Current repeated operator result:

* if frame `3545` is run under FF, the transition tends to desync
* if `3545` is stepped manually or traversed without FF, the transition stays synced
* resuming FF on `3546` or later stays on the good path in current testing

So the best current operator rule is:

* avoid running frame `3545` under FF

### 4. The good/bad split is currently:

Good path:

* `SceneLoad.ActivationComplete` at `3550`
* `Unity.SceneLoaded` at `3550`
* `SceneChanged` at `3550`
* `HeroController.EnterScene` at `3551`

Bad path:

* `SceneLoad.ActivationComplete` at `3551`
* `Unity.SceneLoaded` at `3551`
* `SceneChanged` at `3551`
* `HeroController.EnterScene` at `3552`

That first one-frame slip later grows into the larger control-return delay that the TASer actually feels.

### 5. The larger delay is downstream amplification

The user-facing symptom is often "the Knight got control back several frames late".

The trace currently suggests that this is not the first failure point. Instead:

1. activation completes one render frame later
2. HK's enter-scene flow shifts accordingly
3. that one-frame slip grows into a larger later control-return delay

So the initial root-facing question is the activation-complete slip, not the eventual `AcceptInput` delay.

## What libTAS fast-forward appears to do

Based on the local `v1.4.3` checkout in `/codex/libTAS`:

* FF is mainly a mode bit, not a separate execution mode
* default FF mode skips:
  * frame-boundary sleep
  * audio mixing
* default FF mode does not skip all rendering by default
* when the game is paused and frame-advancing, libTAS does not use the normal FF draw-skipping path

Relevant practical consequences:

* frame-stepping while FF is enabled is still not identical to ordinary playback with FF off
* but it is also not the same as letting the game free-run under FF
* the important difference for this investigation is likely frame pacing and synchronization, not just "drawing less"

### Frame stepping and pause are more subtle than "freeze the game"

The local `v1.4.3` libTAS code strongly suggests that pausing does not simply suspend all game activity equally.

What happens structurally is:

* the child game main thread enters `frameBoundary()`
* it sends `MSGB_START_FRAMEBOUNDARY`
* the program side receives that in `GameLoop::startFrameMessages()`
* the program side sends `MSGN_START_FRAMEBOUNDARY`
* later, the child game main thread waits in `receive_messages(...)` for `MSGN_END_FRAMEBOUNDARY`

When libTAS is paused, the program loop stops issuing normal frame advances. That means the child main thread can end up stalled in the frame-boundary handshake while the rest of the process is not necessarily "globally frozen" in the same way.

This matters because it makes the user-side operator result much easier to believe:

* manually stepping through frame `3545` is not just "same thing, slower"
* it likely changes how much real time / scheduling opportunity Unity loading-side work gets before the next main-thread frame boundary completes

That is a better fit for the observed behavior than the earlier rough idea that pause might somehow let a Unity coroutine continue by itself. Coroutines are still main-thread-driven, but background loading / loading-thread activity can still interact differently with a paused frame-boundary handshake.

An additional concrete reason this is plausible in local `v1.4.3` libTAS:

* sleep/delay wrappers are main-thread-centric
* if the caller is the main thread, sleeps are usually transferred into the deterministic timer
* if the caller is not the main thread, libTAS usually falls back to the real `nanosleep` / `usleep`

So background-thread waiting is not being "fully deterministic in the same way" as the main-thread frame boundary.

That makes the current operator result much easier to believe:

* when the main thread is paused or frame-stepped at the boundary around `3545`
* Unity loading-side threads can still experience real waiting / real scheduling
* therefore the amount of loading-side progress completed before the next main-thread boundary is not the same as in free-running FF

## Unity-specific libTAS behavior that may matter

The local `v1.4.3` libTAS code has explicit Unity-aware behavior:

* Unity games are detected specially
* `frameBoundary()` runs an extra `ThreadSync::detWait()` for Unity
* libTAS has a special concept of a Unity loading thread
* that loading thread is handled specially around `sem_wait()`

This matters because the current failure boundary is in an activation window that is plausibly sensitive to:

* how the main thread is paced across frame boundaries
* how Unity loading-thread synchronization lines up with those frame boundaries
* whether FF changes that pacing just enough to move activation completion from `3550` to `3551`

Two local libTAS details look especially relevant:

* `frameBoundary()` always runs `ThreadSync::detWait()` for Unity games
* the Unity loading thread is treated specially around `sem_wait()`:
  * it calls `ThreadSync::detSignal(true)` before blocking
  * then re-enables sync with `ThreadSync::detInit()` after the wait returns

So this is not just "main thread runs faster under FF". There is explicit per-frame synchronization with Unity-thread behavior, and that synchronization may be exactly what makes frame `3545` so sensitive.

Combined with the sleep-wrapper behavior above, the strongest current libTAS-side interpretation is:

* the main thread is being paced deterministically at frame boundaries
* Unity loading-side threads are still participating through real waits / wakeups plus libTAS's Unity-specific sync points
* the bad case is likely a slightly different alignment between those two systems during the first additive-scene-present render

## Current best interpretation

The leading working model is:

1. Hollow Knight releases its own `SceneLoad` gates at `3544`.
2. On `3545`, the additive destination scene first becomes present in a way visible through `unitySceneCount = 2`.
3. If FF is allowed to run through that exact render, libTAS/Unity/HK timing sometimes lands the later activation-complete boundary on `3551`.
4. If that render is traversed manually or without FF, activation completes on `3550`.
5. The later enter-scene and control-return flow then amplifies the initial one-frame slip.

This is still a hypothesis, but it is narrower and better grounded than the earlier "FF somewhere in the transition is bad" model.

## Updated operator-facing interpretation

The current best practical reading is:

* frame `3545` is the critical render where the additive scene first becomes present
* if FF is allowed to free-run across that frame, the later activation-complete event can slip from `3550` to `3551`
* if the operator manually shepherds the run through `3545`, the later activation-complete event stays on `3550`

That does not yet prove the exact root cause, but it is precise enough to guide both:

* further libTAS code reading
* current TASer workarounds while investigation continues

## What is not yet proven

The current investigation does not yet prove:

* whether the one-frame slip is caused by Unity background loading, Unity activation completion, libTAS Unity-thread sync, or some interaction among them
* whether the same exact threshold generalizes to every bad vertical transition
* whether the best eventual fix belongs in HKTI, libTAS, or nowhere

## Why this matters

If the earliest slip can be made consistent at the activation-complete boundary, the later control-return slip may disappear as a consequence.

That is why the current investigation is focused more on:

* the `3544` to `3550` load/activation window

than on:

* later control-return behavior by itself
