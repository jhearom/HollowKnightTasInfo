#!/usr/bin/env python3
"""Linux installer for HollowKnightTasInfo release payloads."""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
import zipfile
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


APP_ID = "367520"
REQUIRED_PAYLOAD_FILES = (
    "README.md",
    "HollowKnightTasInfo.config",
    "HollowKnightTasInfo.lua",
    "HollowKnightTasInfo_v2.lua",
    "hollow_knight_Data/Managed/Assembly-CSharp.dll",
    "hollow_knight_Data/Managed/original/Assembly-CSharp.dll",
    "hollow_knight_Data/Managed/MonoMod.RuntimeDetour.dll",
    "hollow_knight_Data/Managed/MonoMod.Utils.dll",
    "hollow_knight_Data/Managed/Mono.Cecil.dll",
    "hollow_knight_Data/Managed/Mono.Cecil.Mdb.dll",
    "hollow_knight_Data/Managed/Mono.Cecil.Pdb.dll",
    "hollow_knight_Data/Managed/Mono.Cecil.Rocks.dll",
    "hollow_knight_Data/Managed/Mono.Security.dll",
)
DEFAULT_STEAM_ROOTS = (
    "~/.steam/root",
    "~/.steam/debian-installation",
    "~/.local/share/Steam",
    "~/.var/app/com.valvesoftware.Steam/.local/share/Steam",
    "~/.var/app/com.valvesoftware.Steam/data/Steam",
    "~/snap/steam/common/.local/share/Steam",
)


class InstallError(RuntimeError):
    pass


@dataclass
class SteamCandidate:
    game_dir: Path
    steam_root: Path
    library_dir: Path
    appmanifest: Path
    buildid: str | None = None
    installdir: str | None = None
    branch: str | None = None
    depot_manifests: dict[str, str] = field(default_factory=dict)
    errors: list[str] = field(default_factory=list)


@dataclass
class DiscoveryReport:
    candidates: list[SteamCandidate] = field(default_factory=list)
    checked_roots: list[str] = field(default_factory=list)
    notes: list[str] = field(default_factory=list)


def info(message: str) -> None:
    print(f"[install-linux] {message}", flush=True)


def die(message: str) -> None:
    raise InstallError(message)


def parse_args() -> argparse.Namespace:
    repo_root = Path(__file__).resolve().parent.parent
    default_release = repo_root if (repo_root / "release-manifest.json").is_file() else repo_root / "bin/HK TAS Info Tool"
    parser = argparse.ArgumentParser(
        description="Install a HollowKnightTasInfo release payload into native Linux Hollow Knight.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "If --game-dir is omitted, the installer probes common Linux Steam roots,\n"
            "parses libraryfolders.vdf/appmanifest_367520.acf, and installs only when\n"
            "exactly one unambiguous native Linux Hollow Knight candidate is found.\n"
            "Target inference uses release-manifest Steam build/depot metadata when\n"
            "available. If inference is ambiguous, pass --target explicitly."
        ),
    )
    parser.add_argument("--release", default=str(default_release), help="release directory or zip")
    parser.add_argument("--game-dir", help="explicit Hollow Knight install directory")
    parser.add_argument("--steam-root", action="append", default=[], help="Steam root to probe; may be repeated")
    parser.add_argument("--target", help="release target to install, for example v1432")
    parser.add_argument("--dry-run", action="store_true", help="print actions without modifying the game directory")
    parser.add_argument("--yes", action="store_true", help="do not prompt before installing")
    parser.add_argument("--backup", dest="backup", action="store_true", default=True, help="back up overwritten files first")
    parser.add_argument("--no-backup", dest="backup", action="store_false", help="do not create backups")
    parser.add_argument("--force", action="store_true", help="allow mixed native/Windows shape and config overwrite")
    parser.add_argument("--verbose", action="store_true", help="print discovery and metadata details")
    return parser.parse_args()


def tokenize_vdf(text: str) -> list[str]:
    tokens: list[str] = []
    index = 0
    length = len(text)
    while index < length:
        char = text[index]
        if char.isspace():
            index += 1
            continue
        if char == "/" and index + 1 < length and text[index + 1] == "/":
            newline = text.find("\n", index)
            if newline == -1:
                break
            index = newline + 1
            continue
        if char in "{}":
            tokens.append(char)
            index += 1
            continue
        if char == '"':
            index += 1
            value_chars: list[str] = []
            while index < length:
                current = text[index]
                if current == "\\" and index + 1 < length:
                    value_chars.append(text[index + 1])
                    index += 2
                    continue
                if current == '"':
                    index += 1
                    break
                value_chars.append(current)
                index += 1
            tokens.append("".join(value_chars))
            continue
        start = index
        while index < length and not text[index].isspace() and text[index] not in "{}":
            index += 1
        tokens.append(text[start:index])
    return tokens


def parse_vdf_text(text: str) -> dict[str, Any]:
    tokens = tokenize_vdf(text)
    index = 0

    def parse_object() -> dict[str, Any]:
        nonlocal index
        result: dict[str, Any] = {}
        while index < len(tokens):
            token = tokens[index]
            if token == "}":
                index += 1
                break
            if token == "{":
                die("invalid VDF: unexpected object start")
            key = token
            index += 1
            if index >= len(tokens):
                result[key] = ""
                break
            if tokens[index] == "{":
                index += 1
                result[key] = parse_object()
            else:
                result[key] = tokens[index]
                index += 1
        return result

    parsed = parse_object()
    if index != len(tokens):
        die("invalid VDF: trailing tokens")
    return parsed


def parse_vdf_file(path: Path) -> dict[str, Any]:
    try:
        return parse_vdf_text(path.read_text(encoding="utf-8", errors="replace"))
    except OSError as exc:
        die(f"could not read VDF file {path}: {exc}")


def as_dict(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def steam_roots(extra_roots: list[str]) -> list[Path]:
    roots: list[Path] = []
    for raw in [*extra_roots, *DEFAULT_STEAM_ROOTS]:
        path = Path(raw).expanduser()
        if path not in roots:
            roots.append(path)
    return roots


def library_dirs_from_root(steam_root: Path, report: DiscoveryReport) -> list[Path]:
    library_file = steam_root / "steamapps/libraryfolders.vdf"
    if not library_file.is_file():
        report.notes.append(f"{steam_root}: missing steamapps/libraryfolders.vdf")
        return []

    libraries = [steam_root]
    data = parse_vdf_file(library_file)
    folder_data = as_dict(data.get("libraryfolders"))
    for key, value in folder_data.items():
        candidate: Path | None = None
        if isinstance(value, str):
            candidate = Path(value).expanduser()
        elif isinstance(value, dict):
            raw_path = value.get("path")
            apps = as_dict(value.get("apps"))
            if isinstance(raw_path, str) and (not apps or APP_ID in apps):
                candidate = Path(raw_path).expanduser()
        if candidate and candidate not in libraries:
            libraries.append(candidate)
    return libraries


def appmanifest_candidates(library_dir: Path) -> list[Path]:
    return [library_dir / f"steamapps/appmanifest_{APP_ID}.acf"]


def steam_candidate_from_manifest(steam_root: Path, library_dir: Path, manifest_path: Path) -> SteamCandidate:
    data = parse_vdf_file(manifest_path)
    app_state = as_dict(data.get("AppState"))
    installdir = app_state.get("installdir") if isinstance(app_state.get("installdir"), str) else "Hollow Knight"
    buildid = app_state.get("buildid") if isinstance(app_state.get("buildid"), str) else None
    user_config = as_dict(app_state.get("UserConfig"))
    branch = user_config.get("betakey") if isinstance(user_config.get("betakey"), str) else None

    depot_manifests: dict[str, str] = {}
    installed_depots = as_dict(app_state.get("InstalledDepots"))
    for depot_id, depot_info in installed_depots.items():
        if isinstance(depot_info, dict):
            manifest = depot_info.get("manifest")
            if isinstance(manifest, str) and manifest:
                depot_manifests[depot_id] = manifest

    game_dir = library_dir / "steamapps/common" / str(installdir)
    return SteamCandidate(
        game_dir=game_dir,
        steam_root=steam_root,
        library_dir=library_dir,
        appmanifest=manifest_path,
        buildid=buildid,
        installdir=str(installdir),
        branch=branch,
        depot_manifests=depot_manifests,
    )


def discover_steam_candidates(extra_roots: list[str]) -> DiscoveryReport:
    report = DiscoveryReport()
    seen_manifests: set[Path] = set()
    for root in steam_roots(extra_roots):
        report.checked_roots.append(str(root))
        if not root.exists():
            report.notes.append(f"{root}: does not exist")
            continue
        for library_dir in library_dirs_from_root(root, report):
            for manifest_path in appmanifest_candidates(library_dir):
                manifest_key = manifest_path.resolve() if manifest_path.exists() else manifest_path
                if manifest_key in seen_manifests:
                    continue
                seen_manifests.add(manifest_key)
                if manifest_path.is_file():
                    report.candidates.append(steam_candidate_from_manifest(root, library_dir, manifest_path))
                else:
                    report.notes.append(f"{library_dir}: missing steamapps/appmanifest_{APP_ID}.acf")
    return report


def print_discovery_report(report: DiscoveryReport) -> None:
    info("Steam roots checked:")
    for root in report.checked_roots:
        info(f"  {root}")
    if report.candidates:
        info("Steam Hollow Knight candidates:")
        for candidate in report.candidates:
            info(f"  {candidate.game_dir} (buildid={candidate.buildid or 'unknown'})")
    if report.notes:
        info("Discovery notes:")
        for note in report.notes:
            info(f"  {note}")


def prepare_release(path: Path) -> tuple[Path, tempfile.TemporaryDirectory[str] | None]:
    if path.is_dir():
        return path.resolve(), None
    if not path.is_file():
        die(f"release path does not exist: {path}")
    if path.suffix.lower() != ".zip":
        die(f"release must be a directory or .zip file: {path}")
    temp_dir = tempfile.TemporaryDirectory()
    try:
        with zipfile.ZipFile(path) as archive:
            for member in archive.infolist():
                member_path = Path(member.filename)
                if member_path.is_absolute() or ".." in member_path.parts:
                    die(f"unsafe path in release zip: {member.filename}")
            archive.extractall(temp_dir.name)
    except InstallError:
        temp_dir.cleanup()
        raise
    except zipfile.BadZipFile as exc:
        temp_dir.cleanup()
        die(f"invalid release zip {path}: {exc}")
    return Path(temp_dir.name), temp_dir


def load_manifest(release_dir: Path) -> dict[str, Any]:
    manifest_path = release_dir / "release-manifest.json"
    if not manifest_path.is_file():
        die(f"missing release manifest: {manifest_path}")
    try:
        with manifest_path.open("r", encoding="utf-8") as handle:
            manifest = json.load(handle)
    except json.JSONDecodeError as exc:
        die(f"invalid release manifest JSON: {exc}")
    if not isinstance(manifest.get("targets"), dict):
        die("release manifest is missing object field: targets")
    return manifest


def target_entries(manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    targets = manifest["targets"]
    return {key: value for key, value in targets.items() if isinstance(value, dict)}


def normalize_id_list(value: Any) -> set[str]:
    if not isinstance(value, list):
        return set()
    return {str(item) for item in value}


def infer_target(manifest: dict[str, Any], candidate: SteamCandidate | None) -> tuple[str | None, str]:
    targets = target_entries(manifest)
    if len(targets) == 1:
        only_target = next(iter(targets))
        return only_target, "release manifest contains exactly one target"

    if candidate is None:
        return None, "no Steam metadata is available and release contains multiple targets"

    matches: list[tuple[str, str]] = []
    for name, entry in targets.items():
        build_ids = normalize_id_list(entry.get("knownSteamBuildIds"))
        if candidate.buildid and candidate.buildid in build_ids:
            matches.append((name, f"Steam buildid {candidate.buildid}"))
            continue

        manifest_ids = normalize_id_list(entry.get("knownSteamManifestIds"))
        for depot_id, manifest_id in candidate.depot_manifests.items():
            if manifest_id in manifest_ids:
                matches.append((name, f"Steam depot {depot_id} manifest {manifest_id}"))
                break

    unique = sorted({target for target, _reason in matches})
    if len(unique) == 1:
        reasons = ", ".join(reason for target, reason in matches if target == unique[0])
        return unique[0], reasons
    if len(unique) > 1:
        return None, f"Steam metadata matched multiple targets: {', '.join(unique)}"
    return None, "Steam metadata did not match any known target mapping; pass --target explicitly"


def validate_payload(release_dir: Path, manifest: dict[str, Any], target: str) -> Path:
    targets = target_entries(manifest)
    if target not in targets:
        die(f"target {target} not found in release manifest")
    payload_directory = str(targets[target].get("payloadDirectory") or target)
    payload_dir = release_dir / payload_directory
    if not payload_dir.is_dir():
        die(f"missing payload directory for {target}: {payload_dir}")
    for relative in REQUIRED_PAYLOAD_FILES:
        if not (payload_dir / relative).is_file():
            die(f"missing {target} payload file: {relative}")
    return payload_dir


def verify_checksums(release_dir: Path) -> None:
    checksum_file = release_dir / "SHA256SUMS"
    if not checksum_file.is_file():
        return
    if not shutil.which("sha256sum"):
        die("required command not found: sha256sum")
    info("Verifying release checksums")
    result = subprocess.run(
        ["sha256sum", "-c", "SHA256SUMS"],
        cwd=release_dir,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        die(f"release checksum verification failed: {result.stderr.strip()}")


def validate_game_dir(game_dir: Path, force: bool) -> None:
    if not game_dir.is_dir():
        die(f"game directory does not exist: {game_dir}")
    managed = game_dir / "hollow_knight_Data/Managed"
    if not managed.is_dir():
        die(f"missing Hollow Knight managed directory under: {game_dir}")

    has_native = (game_dir / "hollow_knight.x86_64").is_file()
    has_windows = (game_dir / "hollow_knight.exe").is_file()
    if not has_native and has_windows:
        die("Proton/Windows-style install detected. Use native Linux Hollow Knight under Steam Linux Runtime 1.0 / scout.")
    if not has_native:
        die(f"native Linux executable not found: {game_dir / 'hollow_knight.x86_64'}")
    if has_windows and not force:
        die("both hollow_knight.x86_64 and hollow_knight.exe exist; pass --force only if this is intentionally a native Linux install")


def select_game_dir(args: argparse.Namespace) -> tuple[Path, SteamCandidate | None]:
    if args.game_dir:
        return Path(args.game_dir).expanduser().resolve(), None

    report = discover_steam_candidates(args.steam_root)
    viable_by_path: dict[Path, SteamCandidate] = {}
    for candidate in report.candidates:
        try:
            validate_game_dir(candidate.game_dir, args.force)
        except InstallError as exc:
            candidate.errors.append(str(exc))
        else:
            viable_by_path.setdefault(candidate.game_dir.resolve(), candidate)
    viable = list(viable_by_path.values())

    if args.verbose or len(viable) != 1:
        print_discovery_report(report)
        for candidate in report.candidates:
            for error in candidate.errors:
                info(f"  rejected {candidate.game_dir}: {error}")

    if not viable:
        die("could not auto-discover a valid native Linux Hollow Knight install; pass --game-dir explicitly")
    if len(viable) > 1:
        paths = "\n".join(f"  {candidate.game_dir}" for candidate in viable)
        die(f"multiple valid Hollow Knight installs found; pass --game-dir explicitly:\n{paths}")
    return viable[0].game_dir.resolve(), viable[0]


def confirm_install(target: str, game_dir: Path, assume_yes: bool) -> None:
    if assume_yes:
        return
    reply = input(f"Install target {target} into {game_dir}? [y/N] ")
    if reply not in {"y", "Y", "yes", "YES"}:
        die("installation cancelled")


def install_payload(payload_dir: Path, game_dir: Path, target: str, args: argparse.Namespace) -> None:
    backup_dir: Path | None = None
    if args.backup:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        backup_dir = game_dir / ".hkti-backups" / f"{stamp}-{target}"
        info(f"Backup directory: {backup_dir}")

    copy_plan: list[tuple[Path, Path, str]] = []
    for relative in REQUIRED_PAYLOAD_FILES:
        src = payload_dir / relative
        dst = game_dir / relative
        if relative == "HollowKnightTasInfo.config" and dst.exists() and not args.force:
            info("Preserving existing HollowKnightTasInfo.config; use --force to overwrite it")
            continue
        copy_plan.append((src, dst, relative))

    if args.dry_run:
        for src, dst, _relative in copy_plan:
            if dst.is_file() and args.backup:
                info(f"Would back up {dst}")
            info(f"Would copy {src} -> {dst}")
        return

    if backup_dir is not None:
        for _src, dst, relative in copy_plan:
            if dst.is_file():
                backup_path = backup_dir / relative
                backup_path.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(dst, backup_path)

    for src, dst, _relative in copy_plan:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)


def main() -> int:
    args = parse_args()
    release_temp: tempfile.TemporaryDirectory[str] | None = None
    try:
        release_dir, release_temp = prepare_release(Path(args.release).expanduser())
        manifest = load_manifest(release_dir)
        game_dir, steam_candidate = select_game_dir(args)
        target = args.target
        if not target:
            target, reason = infer_target(manifest, steam_candidate)
            if not target:
                die(reason)
            info(f"Inferred target {target}: {reason}")

        payload_dir = validate_payload(release_dir, manifest, target)
        verify_checksums(release_dir)
        validate_game_dir(game_dir, args.force)

        info(f"Release: {release_dir}")
        info(f"Payload: {payload_dir}")
        info(f"Game dir: {game_dir}")
        info(f"Target: {target}")
        if steam_candidate and args.verbose:
            info(f"Steam appmanifest: {steam_candidate.appmanifest}")
            info(f"Steam buildid: {steam_candidate.buildid or 'unknown'}")
            if steam_candidate.depot_manifests:
                for depot_id, manifest_id in sorted(steam_candidate.depot_manifests.items()):
                    info(f"Steam depot manifest: {depot_id}={manifest_id}")

        if args.dry_run:
            info("Dry run: no files will be modified")
        else:
            confirm_install(target, game_dir, args.yes)
        install_payload(payload_dir, game_dir, target, args)
        info("Install flow complete")
        return 0
    except InstallError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    finally:
        if release_temp is not None:
            release_temp.cleanup()


if __name__ == "__main__":
    sys.exit(main())
