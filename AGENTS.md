# Project Agent Instructions (HollowKnightTasInfo)

## Scope and Source of Truth
- Primary codebase: `/codex/HollowKnightTasInfo`.
- Main C# tooling project: `/codex/HollowKnightTasInfo/Assembly-CSharp.TasInfo.mm.csproj`.
- Main runtime/tooling sources live under `/codex/HollowKnightTasInfo/Source`.
- Patch hooks live under `/codex/HollowKnightTasInfo/Patches`.
- Version-specific game references live under `/codex/HollowKnightTasInfo/lib`.
- Lua-side integration files live at repo root (`HollowKnightTasInfo.lua`, `HollowKnightTasInfo_v2.lua`) and are part of the libTAS integration surface, not incidental helper scripts.
- Nearby repositories such as `/codex/ModdingAPI`, `/codex/HollowKnight.DebugMod`, `/codex/ReplayTimerMod`, and `/codex/hollow_knight_analysis` may be read for reference when useful or user-directed, but treat them as read-only unless the user explicitly expands scope.
- `/codex/libTAS` may be read as a general libTAS reference codebase, but it is read-only and must never be pushed to or modified from this workflow.
- The pinned libTAS reference version for this workspace is `v1.4.3`, matching the user's stated runtime baseline.
- Prefer the local `/codex/libTAS` checkout at tag `v1.4.3` for version-sensitive libTAS behavior, Lua integration expectations, and feature availability.
- Do not assume `/codex/libTAS` `master` matches the user's TAS VM unless the user explicitly asks to compare against a newer libTAS revision.

## OS Context and Runtime Target (Required)
- Development/agent environment is Linux.
- This project targets Hollow Knight running under libTAS on Linux.
- libTAS runs a Lua engine and may autoload or explicitly run the repo's Lua scripts for integration behavior, OSD display, and tooling coordination.
- Build and packaging outputs should remain compatible with the Linux/libTAS workflow described in `README.md`.
- Avoid assumptions that the tooling is a normal mod-loader plugin; this repo patches `Assembly-CSharp.dll` directly and ships supporting runtime injection files.
- Treat Lua changes as runtime-affecting integration changes, not documentation-only edits.
- Prefer repo-local outputs and packaging artifacts over writing directly into a live game install unless the user explicitly asks for deployment/testing steps.

## Project Goal
- This repo implements Hollow Knight TAS support tooling and Lua integration for libTAS.
- Preserve determinism, sync stability, and established TAS workflows unless the user explicitly asks for behavior changes.
- Treat the tooling's authoritative internal state and timing model as the source of truth; compatibility/export layers should adapt from that model rather than distorting it.
- Keep version-specific behavior isolated where practical, especially for `v1028`, `v1028_Krythom`, `v1221`, and `v1432`.

## Working Style
- Keep patches concise and behavior-scoped.
- Prefer minimal-risk changes over broad refactors in timing, RNG sync, playback, input logging, MultiSync, and patched game hooks.
- If uncertain, add diagnostics, guards, or validation steps instead of speculative timing or sync logic changes.
- Preserve existing on-disk/log/export formats unless compatibility or migration is handled intentionally.

## Build and Validation
- The main project supports configurations:
  - `v1028`
  - `v1028_Krythom`
  - `v1221`
  - `v1432`
- Typical local build command:
  - `DOTNET_CLI_HOME=/tmp/dotnet_home NUGET_PACKAGES=/tmp/nuget dotnet build /codex/HollowKnightTasInfo/Assembly-CSharp.TasInfo.mm.csproj -c v1221`
- Substitute the configuration name as needed for other game versions.
- Post-build packaging is driven by the project file and writes release artifacts under:
  - `/codex/HollowKnightTasInfo/bin/HK TAS Info Tool`
  - `/codex/HollowKnightTasInfo/bin/HK_TAS_Info_Tool_v<version>.zip`
- The project expects version-specific managed references already present under `lib/<configuration>/`.
- When reporting validation, state exactly which configuration(s) were built or tested and whether the result is compile-time, post-build packaging, or runtime/libTAS validation only.

## Git and Tracking
- Use GitHub issues on the fork repo `jhearom/HollowKnightTasInfo` for implementation tracking when the user wants issue-based execution.
- Keep issue titles/body explicit about patch/configuration scope and intended user-visible behavior.
- Milestone updates should stay concise and operational:
  - scope,
  - approach,
  - files/areas touched,
  - validation status,
  - blockers or follow-up work.
- Do not create, edit, or comment on upstream `Jarlyk/HollowKnightTasInfo`.

## GitHub Comment Formatting (Required)
- When posting or editing GitHub issue/PR comments via `gh`, use real multiline Markdown bodies.
- Do not pass escaped newline sequences in inline `--body` strings.
- Prefer `--body-file` with a temporary file or stdin containing real newlines.
- After posting or editing, verify the rendered/stored body does not contain literal `\\n`; if it does, fix it immediately.

## Remote and Push Policy (Required)
- Treat `origin` (`jhearom/HollowKnightTasInfo`) as the only writable remote.
- Treat `upstream` (`Jarlyk/HollowKnightTasInfo`) as fetch-only.
- Never push to `upstream`.
- Pushing to `origin` is allowed only if the user explicitly asks for it.
- Do not open PRs unless the user explicitly asks for one.

## Repo Boundary Rule (Required)
- Do not modify repositories other than `/codex/HollowKnightTasInfo` unless the user explicitly changes scope.
- Reading adjacent repositories for reference is allowed when helpful, but treat them as read-only by default.
- If a requested change appears to require coordinated edits in another repo, stop and ask before patching both sides.

## Privilege Escalation and Sudo Policy (Required)
- Do not run `sudo` directly.
- If root or system-level changes are needed, tell the user the exact command to run, then continue with non-root steps after confirmation.

## Execution Logging and State Hygiene (Required)
- Treat substantial tasks as compaction-prone and persist concise local state at meaningful checkpoints.
- When working against an existing issue, log the intended approach and acceptance checks before the first significant code change.
- During implementation, log meaningful progress events when issue tracking is in use:
  - milestone reached,
  - important discovery,
  - validation result,
  - blocker,
  - scope/plan adjustment.
- At the end of a work slice, capture:
  - what changed,
  - configuration(s) affected,
  - build/runtime validation status,
  - known limitations,
  - next recommended action.

## Compaction Handoff Procedure (Required)
- Maintain a local handoff file at `/codex/HollowKnightTasInfo/.codex/COMPACTION_HANDOFF.md`.
- This handoff file is local-only and must not be committed.
- Refresh it before ending a substantial work session, after meaningful milestones or plan adjustments, and before any context-compaction handoff.
- Include at minimum:
  - active branch,
  - current issue links if any,
  - configurations touched,
  - current build/runtime status,
  - known blockers or risks,
  - explicit next actions and useful commands.
