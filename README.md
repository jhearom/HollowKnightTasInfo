# Hollow Knight TAS Tooling

This project provides tooling that runs within the Linux-based Hollow Knight instance and provides information
to a lua script running on the libTAS side to improve the experience of creating HK TASes.  This is a fork
of DemoJameson's original tooling and includes a lot of additional features.  These features are far more
invasive than the original tooling and are not compatible with syncing the unmodified game.  They do, however,
provide mitigations against desyncs, as well as a variety of other more niche features.

## Installation

The release distribution is a zip file that contains one or more HK version numbers.  Each corresponds to
a base dll that was used for insatalling the tooling.  If building the tooling from source, this will be
created in the `bin/HK TAS Info Tool` folder.

To install, choose the version that corresponds to the version of HK you are using and copy the contents into
the main folder of your HK installation.  This will overwrite the `Assembly-CSharp.dll` under `hollow_knight_Data/Managed`
as well as copy in some Monomod runtime injection dlls.  It will also place a `HollowKnightTasInfo.config` file
as well as a `HollowKnightTasInfo.lua` file in the main folder of your HK installation.  These files are used
for integration on the libTAS side.

##  Basic Usage

While the game is running in libTAS, click on the `Tools | Lua Console...` menu item.  From there, click
the `Script | Add script file` menu item.  This will bring up a file dialog; choose the `HollowKnightTasInfo.lua` 
or `HollowKnightTasInfo_v2.lua` file that was copied into your HK installation.  The first is used for backwards 
compatibility with older patches of libTAS, while the latter supports additional features like disabling fast forward 
during loads.  The latter only works in versions of libTAS at interim build f69140c or later.  The tooling is now running 
and will provide information to the libTAS OSD.  It only loads after the main menu is loaded, so if you don't see anything, 
allow the game to run until the main menu loads.  If you still don't see anything, make sure the `Video | OSD | Lua` 
option is enabled.  Any setting changes will require stepping forward at least one frame to be visible.

While the tooling is running, you can update the configuration at any time by editing the `HollowKnightTasInfo.config`
file at runtime.  The tooling will automatically reload the configuration file when it detects a change.  This allows
for toggling various features (like hitbox display and various text entries) at any time during TASing.  If you make
a mistake while editing the config file, like entering text for a numeric value, it's possible that this might cause 
the tooling to crash.  If this happens, you can fix this by fixing the config file and then reverting to a prior
savestate.

## Configuration Parameters

Editing the configuration file is the primary way to interact with the tooling.  The following parameters are supported:

* `Enabled`: If set to false, will disable the changes that impact the display.  This is effectively a way
to quickly override the various other display parameters without having to edit them individually.
* `ShowTimeOnly`: If set to true, will suppress all of the text in the upper right corner except for Time
and UnscaledTime, if enabled.  This can be used as an easy to embed the loadless timer into an encoded video.
* `ShowCustomInfo`: Whether to display various custom info in the text area.  The custominfo feature is described 
in more detail later in this document.
* `ShowKnightInfo`: Whether to display various knight state flags information, as well as
position and velocity.  Note that when interpreting state flags, they apply to the most recently rendered
frame.  This means if you want to attack on the earliest possible frame, you should send the input on the
frame just prior to the flag appearing in the OSD.
* `ShowSceneName`: Whether to display the name of the currently loaded scene.  In a glitched context with multiple
scenes loaded, note that this displays the name of the scene that `GameManager` thinks is loaded.
* `ShowTime`: Whether to display the current load-removed elapsed time.  This should match the LRT tracked by
the LiveSplit autosplitter.
* `ShowUnscaledTime`: Whether to show real time elapsed from the start of the game.  This is primarily useful
in the context of the MultiSync feature, which is described later in this document.
* `ShowTimeMinusFixedTime`: Whether to display the T-FT for the most recent frame.  This is very useful
in various advanced TASing contexts for HK, since a wide variety of subtle game behaviors are tied to the T-FT value.
* `ShowRng`: Whether to show information on the most recent RNG state.  This tells you how frequently the RNG
is being advanced, which makes it easier to understand what actions are manipulating RNG.
* `ShowEnemyHp`: Whether to display current enemy HP near their hitboxes.  This works for the vast majority of
enemies, though there are some niche exceptions like Watcher Knights in some states.
* `ShowEnemyPosition`: Whether to display the position of enemies near their hitboxes.  This is useful when
attempting to do very precise control of positioning by manipulating enemy movements.
* `ShowEnemyVelocity`: Whether to display the velocity of enemies near their hitboxes.  This is mostly useful
for manipulating the RNG movement of flying enemies to be able to predict how they will move.
* `ShowHitbox`: Whether to display standard hitboxes, including collision hitboxes, enemy hitboxes, 
nail hitboxes, and player hitboxes.
* `ShowOtherHitbox`: Whether to display less frequently used hitboxes, including triggers, background objects
and various enemy sensor boxes.  This can slow down libTAS performance in scenes with a very large number
of hitboxes, so it's recommended to disable this when encoding or catching up.
* `ShowGroundedTime`: Whether to display grounded time and frames.  This was primarily added for the gimmick
of "The Floor is Lava" TAS, but it's available if anyone else wants to monitor grounded time for some reason.
* `PositionPrecision`: The number of decimal places to display for position.
* `VelocityPrecision`: The number of decimal places to display for velocity.
* `ForceGatheringSwarm`: When true, this gives the knight the Gathering Swarm charm and forces it to be equipped.
This was primarily intended for the "0 geo with Gathering Swarm" TAS, but it's available if anyone else wants
to use it.
* `GiveLantern`: If true, this gives the lantern ability to the knight.  This is primarily a quality of life
feature for dark rooms to improve the viewing experience in the encoded video.  Note that this flag will
cause desyncs if changed, so it can't be toggled mid-TAS at will.
* `PauseTimer`: While true, this explicitly pauses the loadless timer.  This can be useful when doing spliced TASes
where you need to start the timer at a specific point in time in the encoding.
* `CameraZoom`: The camera zoom level.  This is useful when TASing large rooms where you want to be able to
see distant enemies or colliders.  This is implemented in such a way that it shouldn't cause desyncs, so you
can freely change this while TASing.
* `CameraFollow`: While true, will lock the camera to track the knight's center position.  This is helpful
when doing inventory drops or aquiring far more horizontal momentum than the game intends you to have, as
a way to more easily see what's happening.
* `DisableCameraShake`: Whether to disable camera shake.  This improves the TASing experience and possibly
the viewing experience of the final encoded video if you don't want the shake.
* `StartingGameTime`: The starting value of the loadless timer (in seconds) when either loading into KP or the
save file scene.  This is useful when doing segmented runs where you want to sync the timer with the end
of a previous segment.  This can also be set to a negative value if you want to synchronize the start of
timing with an event in the middle of the TAS.
* `StartingSoul`: The amount of soul to give the knight upon receiving a '[' input.  This can be used
in a segmented TAS to match the soul from a previous segment.
* `StartingHealth`: The amount of health to give the knight upon receiving a '[' input.  This can be used
in a segmented TAS to match the health from a previous segment.
* `RecordMultiSync`: Whether to record synchronization information for use by the MultiSync feature.  This
feature is described in more detail later in this document.
* `MultiSyncName`: The name to use for the MultiSync recording.  This is helpful when coordinating between
several TAS instances to keep track of which recording maps to which knight.
* `MultiSyncConsolidateGeo`: Whether to consolidate geo updates when recording synchronization information.  
When this is false, every individual piece of geo gets its own entry.
* `DisableFFDuringLoads`: Whether to disable fast forward during loads.  This is only supported in libTAS interim builds
after commit f69140c using the newer lua script.  This can help improve sync stability, particularly on patch 1432.  Take
care when setting savestates near to loads while using this feature, as a savestate inside a non-FF zone might
preempt the fast forward protection, especially if immediately adjacent to the actual scene change.
* `RecordReplayExport`: Whether to record ReplayTimerMod-compatible room exports.  This is checked when a new room begins,
so changing it mid-room will not start or stop a partial recording.  If you enable it during a room, recording begins on
the next room.  If you disable it during a room, the current room finishes normally and later rooms stop recording.

## Logging features

The tooling supports a variety of logging features that can be used to understand what happened over
the course of a movie, as well as giving the context required to reuse movement by syncing T-FT.  Logs can
be exported by making sure writing to disk is enabled in libTAS, then sending an '=' input.  In particular,
the libTAS option that prevents writing to disk must be off or these exports will silently fail.  This will
create a few folders in the game's directory:

* `Diagnostics`: This contains diagnostics logs organized by scene.  This includes frame numbers relative to scene
start, T-FT for each frame, a few state flags, knight's position and velocity and the inputs sent on the frame.
* `Inputs`: This contains the inputs in the movie file, broken down by scene.  The inputs are in the format used
by libTAS and can thus be pasted into the Input Editor.  This can be used for splicing at the movie level, with the
caveat that T-FT needs to be manually synced and RNG will need to be synced via the RNG Synchronization feature.
* `Recording`: This will contain the `MultiSync{Name}.txt` recording (if enabled), as well as the RNG per scene in the
`RNG` subfolder.  When splicing, these RNG values need to be matched to their corresponding file in Inputs.
* `Recording/ReplayTimerMod`: This contains ReplayTimerMod-compatible replay exports written when you send an `=` input.
  Exports are incremental: each `=` dump includes only the rooms completed since the previous replay-export dump.  Each
  dump contains:
  * per-room `RTM3` text files under `Recording/ReplayTimerMod/Dump_.../Rooms`
  * a collection-style `RTMC1` text file
  * RTM-native per-scene JSON under `Recording/ReplayTimerMod/Dump_.../ReplayMod/data`
  The `ReplayMod/data/*.json` files can be copied directly into ReplayTimerMod's own `ReplayMod/data` directory for testing.
  The current exporter records route keys, RTM-compatible room times, 30 Hz position/facing samples, and hero animation
  clip/frame data.  In local validation, these JSON exports have been sufficient to produce animated knight ghosts in
  ReplayTimerMod on patch 1578 from TAS tooling recordings generated on patch 1432.
  The `RecordReplayExport` config key controls whether new rooms are recorded for export.  It is checked only on room
  boundaries, so enabling it mid-room waits until the next room and disabling it mid-room lets the current room finish.

## ReplayTimerMod Export Notes

The ReplayTimerMod export path is a compatibility feature for racing TAS ghosts against the standalone ReplayTimerMod mod.
The exporter targets ReplayTimerMod's existing on-disk scene JSON format rather than introducing a TAS-specific replay
format.

Important details:

* The exporter is intentionally tied to patch `1432`, because that is the practical Hollow Knight patch for libTAS use.
  The compatibility target on the consumer side is ReplayTimerMod on patch `1578`.
* Exported room timing is intended to match ReplayTimerMod semantics rather than the TAS overlay's own timer.  This keeps
  ReplayTimerMod metadata and route ordering behavior predictable.
* The exporter writes ReplayTimerMod-compatible animation streams.  If clip resolution ever fails for a sample, the
  consumer mod can still fall back to its non-sprite ghost rendering.
* `snapshotId` values in the generated scene JSON are exporter-generated identifiers.  They are only used by ReplayTimerMod
  for per-snapshot selection/editing state and do not affect ghost movement itself.

## RNG Synchronization

Hollow Knight has particularly unstable RNG, which normally makes input splicing effectively impossible and can cause
desyncs, especially on patch 1432.  The tooling supports synchronizing RNG on a scene by scene basis, which
can, in principle, be used for splicing.  This involves recording every RNG call in the game and creating a log file
in a `Recording/RNG` folder as described above.  If the game then finds a `Playback/RNG` folder, it will then
attempt to play back these RNG values while the game is running.

One additional feature is that you can send a ']' input to disable RNG sync playback for the next scene
and advance RNG by one step.  This is primarily intended as a way to more easily reroll room RNG if you'd prefer
not to insert manipulation for scene-level RNG throughout the prior scene.

## MultiSync

This feature is intended as a way to synchronize progress between multiple TASes.  It was originally created for
the "Happy Couple Duo" TAS, which involved two TASes running simultaneously.  The idea here is that you record
state flags for one or more TASes, then play back those state flags on the other TASes.  A wide variety of state
is tracked, including geo, items, abilities, charms, dreamers, bosses, levers, geo rocks, elevators, and stags.

To record state, you need to enable `RecordMultiSync` and set a `MultiSyncName` in the config.  Then, you can send a
a `=` input to output a loog of the sync up until that point in the movie.  This will be written to a file in
the `Recording` folder and will be named `MultiSync{Name}.txt`.

To play back state, copy one or more of these MultiSync text files into a `Playback` folder in the game's
main folder.  If this file exists, state will automatically be updated as the game progresses to match 
the updates in the file.  The file can be updated mid-TAS and libTAS will automatically load the new file,
but it will only apply changes that are in the future relative to the current frame; any changes to past
state will be ignored.

## Custom Info Text

In the config file, under [CustomInfoTemplate], you can specify various custom text to display in the upper right info.
The format can be inferred somewhat from the commented out example entries in the config file, but here are the
major features:

* All entries can specify fixed text as well as a bound value.  The bound value is in curly braces.  For example,
`paused: {GameManager.isPaused}` will display whether the game is paused.
* Fields can be referenced using `{ClassName.fieldName}`, where `GameManager`, `HeroController`, 
`PlayerData` and `HeroControllerStates` all have special support to find them throuugh the active `GameManager` instance.
* Other classes are found using `Object.FindObjectdOfType`.
* In general, you can pass simple methods in place of fields and it will call them.  For example, 
`canAttack: {PlayerData.CanAttack()}`.
* For game objects, you can specify the game object name and it will call `GameObject.Find` internally to find it.  For
example, `crawler hp: {Crawler Fixed.LocateMyFSM(health_manager_enemy).FsmVariables.FindFsmInt(HP)}` will look for
an object named `Crawler` and then look up the HP variable on its health_manager_enemy FSM.  The supported methods here
are `LocateMyFSM` and `GetComponentInChildren`.

## Acknowedgements

* Thanks to Kilaye for creating the libTAS tool, which makes all of this possible
* Thanks to DemoJameson for the initial version of the HK TAS tooling on which this was based
* Thanks to all of my fellow TASers on the Hollow Knight TAS Discord, both for direct contributions 
to the tooling and also for general feedback and suggestions

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
