#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
readonly APP_ID="367520"
readonly DEFAULT_TARGETS=("v1028" "v1028_Krythom" "v1221" "v1432")
readonly KNOWN_UNSUPPORTED_VERSIONS=("1.5.12620" "1.5.78.11833")
readonly DEFAULT_STEAM_ROOTS=(
  "$HOME/.steam/root"
  "$HOME/.steam/debian-installation"
  "$HOME/.local/share/Steam"
  "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
  "$HOME/.var/app/com.valvesoftware.Steam/data/Steam"
  "$HOME/snap/steam/common/.local/share/Steam"
)
readonly REQUIRED_PAYLOAD_FILES=(
  "README.md"
  "HollowKnightTasInfo.config"
  "HollowKnightTasInfo.lua"
  "HollowKnightTasInfo_v2.lua"
  "hollow_knight_Data/Managed/Assembly-CSharp.dll"
  "hollow_knight_Data/Managed/original/Assembly-CSharp.dll"
  "hollow_knight_Data/Managed/MonoMod.RuntimeDetour.dll"
  "hollow_knight_Data/Managed/MonoMod.Utils.dll"
  "hollow_knight_Data/Managed/Mono.Cecil.dll"
  "hollow_knight_Data/Managed/Mono.Cecil.Mdb.dll"
  "hollow_knight_Data/Managed/Mono.Cecil.Pdb.dll"
  "hollow_knight_Data/Managed/Mono.Cecil.Rocks.dll"
  "hollow_knight_Data/Managed/Mono.Security.dll"
)

release_path="${REPO_ROOT}/bin/HK TAS Info Tool"
if [[ -f "${SCRIPT_DIR}/release-manifest.json" ]]; then
  release_path="${SCRIPT_DIR}"
elif [[ -f "${REPO_ROOT}/release-manifest.json" ]]; then
  release_path="${REPO_ROOT}"
fi
game_dir=""
target=""
dry_run=false
assume_yes=false
backup=true
force=false
verbose=false
tmp_dir=""
release_dir=""
payload_dir=""
steam_candidate_buildid=""
steam_candidate_manifest_ids=""
steam_roots=()
proton_candidates=()

usage() {
  cat <<'EOF'
Usage: install-linux.sh [options]

Install a HollowKnightTasInfo release payload into native Linux Hollow Knight.

Options:
  --release PATH         Release directory or zip. Defaults to the extracted release root, or bin/HK TAS Info Tool in a checkout.
  --game-dir PATH        Explicit Hollow Knight install directory.
  --steam-root PATH      Steam root to probe. May be repeated.
  --target TARGET        Release target to install, for example v1432.
  --dry-run              Print actions without modifying the game directory.
  --yes                  Do not prompt before installing.
  --backup               Back up overwritten files first. Enabled by default.
  --no-backup            Do not create backups.
  --force                Allow mixed native/Windows shape and config overwrite.
  --verbose              Print discovery and metadata details.
  -h, --help             Show this help.

If --game-dir is omitted, the installer probes common Linux Steam roots and
parses libraryfolders.vdf/appmanifest_367520.acf. If discovery is ambiguous,
pass --game-dir explicitly.

If --target is omitted, inference only succeeds for a single-target release or
when Steam build/depot manifest metadata matches release-manifest mappings.
EOF
}

die() {
  printf 'error: %s\n' "$*" >&2
  exit 1
}

info() {
  printf '[install-linux] %s\n' "$*"
}

debug() {
  if [[ "${verbose}" == true ]]; then
    info "$*"
  fi
  return 0
}

cleanup() {
  if [[ -n "${tmp_dir}" && -d "${tmp_dir}" ]]; then
    rm -rf "${tmp_dir}"
  fi
}
trap cleanup EXIT

require_command() {
  command -v "$1" >/dev/null 2>&1 || die "required command not found: $1"
}

absolute_path() {
  local path="$1"
  if [[ -d "${path}" ]]; then
    (cd "${path}" && pwd -P)
  else
    local dir
    local base
    dir="$(dirname -- "${path}")"
    base="$(basename -- "${path}")"
    (cd "${dir}" && printf '%s/%s\n' "$(pwd -P)" "${base}")
  fi
}

vdf_value() {
  local key="$1"
  local file="$2"
  sed -n "s/^[[:space:]]*\"${key}\"[[:space:]]*\"\\(.*\\)\"[[:space:]]*$/\\1/p" "${file}" | head -n 1
}

json_string_value_in_target() {
  local target_name="$1"
  local key="$2"
  local manifest="${release_dir}/release-manifest.json"
  sed -n "/^[[:space:]]*\"${target_name}\"[[:space:]]*:[[:space:]]*{/,/^[[:space:]]*}/p" "${manifest}" |
    sed -n "s/^[[:space:]]*\"${key}\"[[:space:]]*:[[:space:]]*\"\\([^\"]*\\)\".*/\\1/p" |
    head -n 1
}

json_array_values_in_target() {
  local target_name="$1"
  local key="$2"
  local manifest="${release_dir}/release-manifest.json"
  sed -n "/^[[:space:]]*\"${target_name}\"[[:space:]]*:[[:space:]]*{/,/^[[:space:]]*}/p" "${manifest}" |
    sed -n "s/^[[:space:]]*\"${key}\"[[:space:]]*:[[:space:]]*\\[\\(.*\\)\\].*/\\1/p" |
    tr ',' '\n' |
    sed -n 's/^[[:space:]]*"\([^"]*\)"[[:space:]]*$/\1/p'
}

json_array_values_in_unsupported_version() {
  local version="$1"
  local key="$2"
  local manifest="${release_dir}/release-manifest.json"
  sed -n "/^[[:space:]]*\"${version}\"[[:space:]]*:[[:space:]]*{/,/^[[:space:]]*}/p" "${manifest}" |
    sed -n "s/^[[:space:]]*\"${key}\"[[:space:]]*:[[:space:]]*\\[\\(.*\\)\\].*/\\1/p" |
    tr ',' '\n' |
    sed -n 's/^[[:space:]]*"\([^"]*\)"[[:space:]]*$/\1/p'
}

builtin_unsupported_steam_build_ids() {
  case "$1" in
    1.5.12620) printf '%s\n' "22529139" ;;
    1.5.78.11833) printf '%s\n' "20231655" ;;
  esac
}

builtin_unsupported_steam_manifest_ids() {
  case "$1" in
    1.5.12620) printf '%s\n' "708613018541602983" ;;
    1.5.78.11833) printf '%s\n' "5829533265112705522" ;;
  esac
}

manifest_targets() {
  local manifest="${release_dir}/release-manifest.json"
  local target_name
  for target_name in "${DEFAULT_TARGETS[@]}"; do
    if grep -q "^[[:space:]]*\"${target_name}\"[[:space:]]*:" "${manifest}"; then
      printf '%s\n' "${target_name}"
    fi
  done
}

print_supported_targets() {
  local target_name
  local label
  printf 'Supported Hollow Knight versions in this release:\n' >&2
  while IFS= read -r target_name; do
    [[ -n "${target_name}" ]] || continue
    label="$(json_string_value_in_target "${target_name}" "patchLabel")"
    if [[ -n "${label}" ]]; then
      case "${target_name}" in
        v1028_Krythom) printf '  %s (%s special build)\n' "${target_name}" "${label}" >&2 ;;
        *) printf '  %s (%s)\n' "${target_name}" "${label}" >&2 ;;
      esac
    else
      printf '  %s\n' "${target_name}" >&2
    fi
  done < <(manifest_targets)
}

prepare_release_dir() {
  local path="$1"
  if [[ -d "${path}" ]]; then
    release_dir="$(absolute_path "${path}")"
    return 0
  fi

  [[ -f "${path}" ]] || die "release path does not exist: ${path}"
  [[ "${path}" == *.zip ]] || die "release must be a directory or .zip file: ${path}"
  require_command unzip
  tmp_dir="$(mktemp -d)"
  while IFS= read -r entry; do
    case "${entry}" in
      /*|../*|*/../*) die "unsafe path in release zip: ${entry}" ;;
    esac
  done < <(unzip -Z1 "${path}")
  unzip -q "${path}" -d "${tmp_dir}"
  release_dir="${tmp_dir}"
}

validate_release_manifest() {
  [[ -f "${release_dir}/release-manifest.json" ]] || die "missing release manifest: ${release_dir}/release-manifest.json"
  if ! grep -q '"targets"[[:space:]]*:' "${release_dir}/release-manifest.json"; then
    die "release manifest is missing targets"
  fi
}

validate_payload() {
  local selected_target="$1"
  local payload_directory
  local file

  if ! manifest_targets | grep -qx "${selected_target}"; then
    die "target ${selected_target} not found in release manifest"
  fi

  payload_directory="$(json_string_value_in_target "${selected_target}" "payloadDirectory")"
  [[ -n "${payload_directory}" ]] || payload_directory="${selected_target}"
  payload_dir="${release_dir}/${payload_directory}"
  [[ -d "${payload_dir}" ]] || die "missing payload directory for ${selected_target}: ${payload_dir}"

  for file in "${REQUIRED_PAYLOAD_FILES[@]}"; do
    [[ -f "${payload_dir}/${file}" ]] || die "missing ${selected_target} payload file: ${file}"
  done
}

verify_checksums() {
  if [[ -f "${release_dir}/SHA256SUMS" ]]; then
    require_command sha256sum
    info "Verifying release checksums"

    local expected
    local relative_path
    local actual
    local failed=false
    while read -r expected relative_path; do
      [[ -n "${expected}" && -n "${relative_path}" ]] || continue
      relative_path="${relative_path#\*}"
      if [[ "${relative_path}" == /* || "${relative_path}" == ../* || "${relative_path}" == */../* ]]; then
        die "unsafe path in SHA256SUMS: ${relative_path}"
      fi
      if [[ ! -f "${release_dir}/${relative_path}" ]]; then
        printf 'error: checksum file missing: %s\n' "${relative_path}" >&2
        failed=true
        continue
      fi
      actual="$(sha256sum "${release_dir}/${relative_path}" | awk '{print $1}')"
      if [[ "${actual}" != "${expected}" ]]; then
        printf 'error: checksum mismatch: %s\n' "${relative_path}" >&2
        printf 'error:   expected: %s\n' "${expected}" >&2
        printf 'error:   actual:   %s\n' "${actual}" >&2
        failed=true
      fi
    done <"${release_dir}/SHA256SUMS"

    [[ "${failed}" != true ]] || exit 1
  fi
}

validate_game_dir() {
  local check_dir="$1"
  [[ -d "${check_dir}" ]] || return 10
  [[ -d "${check_dir}/hollow_knight_Data/Managed" ]] || return 11

  local has_native=false
  local has_windows=false
  [[ -f "${check_dir}/hollow_knight.x86_64" ]] && has_native=true
  [[ -f "${check_dir}/hollow_knight.exe" ]] && has_windows=true

  if [[ "${has_native}" != true && "${has_windows}" == true ]]; then
    return 12
  fi
  if [[ "${has_native}" != true ]]; then
    return 13
  fi
  if [[ "${has_windows}" == true && "${force}" != true ]]; then
    return 14
  fi
  return 0
}

game_dir_error() {
  case "$1" in
    10) printf 'game directory does not exist' ;;
    11) printf 'missing hollow_knight_Data/Managed' ;;
    12) printf 'Proton/Windows-style install detected. Use native Linux Hollow Knight instead.' ;;
    13) printf 'native Linux executable not found: hollow_knight.x86_64' ;;
    14) printf 'both hollow_knight.x86_64 and hollow_knight.exe exist; pass --force only if this is intentionally a native Linux install' ;;
    *) printf 'unknown validation failure' ;;
  esac
}

print_linux_runtime_instructions() {
  cat >&2 <<'EOF'

To switch Hollow Knight from Proton/Windows to the native Linux build:
  1. Open Steam.
  2. Go to Library.
  3. Right-click Hollow Knight, then click Properties.
  4. Open Compatibility.
  5. Prefer unchecking "Force the use of a specific Steam Play compatibility tool".
     If you must force a tool, select "Steam Linux Runtime 1.0 (scout)".
  6. Close Properties.
  7. Open Hollow Knight's Manage/gear menu, then Installed Files.
  8. Click "Verify integrity of game files", or uninstall/reinstall Hollow Knight if Steam does not replace the files.
  9. Re-run this installer after Steam downloads the native build.

Expected native executable after the switch:
  hollow_knight.x86_64

If "Steam Linux Runtime 1.0 (scout)" is missing, install it with:
  steam steam://install/1070560
EOF
}

steam_library_paths() {
  local root="$1"
  local library_file="${root}/steamapps/libraryfolders.vdf"
  [[ -f "${library_file}" ]] || return 0
  printf '%s\n' "${root}"
  sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "${library_file}"
}

discover_game_dir() {
  local roots=()
  local root
  local library
  local manifest
  local install_dir
  local candidate
  local valid_candidates=()
  local seen_candidates="|"

  if ((${#steam_roots[@]} > 0)); then
    roots=("${steam_roots[@]}" "${DEFAULT_STEAM_ROOTS[@]}")
  else
    roots=("${DEFAULT_STEAM_ROOTS[@]}")
  fi

  [[ "${verbose}" == true ]] && info "Steam roots checked:"
  for root in "${roots[@]}"; do
    [[ "${verbose}" == true ]] && info "  ${root}"
    if [[ ! -d "${root}" ]]; then
      debug "  ${root}: does not exist"
      continue
    fi
    if [[ ! -f "${root}/steamapps/libraryfolders.vdf" ]]; then
      debug "  ${root}: missing steamapps/libraryfolders.vdf"
      continue
    fi

    while IFS= read -r library; do
      [[ -n "${library}" ]] || continue
      manifest="${library}/steamapps/appmanifest_${APP_ID}.acf"
      if [[ ! -f "${manifest}" ]]; then
        debug "  ${library}: missing steamapps/appmanifest_${APP_ID}.acf"
        continue
      fi
      install_dir="$(vdf_value "installdir" "${manifest}")"
      [[ -n "${install_dir}" ]] || install_dir="Hollow Knight"
      candidate="$(absolute_path "${library}/steamapps/common/${install_dir}")"
      case "${seen_candidates}" in
        *"|${candidate}|"*) continue ;;
      esac
      seen_candidates="${seen_candidates}${candidate}|"

      if validate_game_dir "${candidate}"; then
        valid_candidates+=("${candidate}|${manifest}")
        debug "  candidate ${candidate}"
      else
        local status=$?
        if ((status == 12 || status == 14)); then
          proton_candidates+=("${candidate}")
        fi
        debug "  rejected ${candidate}: $(game_dir_error "${status}")"
      fi
    done < <(steam_library_paths "${root}")
  done

  if ((${#valid_candidates[@]} == 0)); then
    if ((${#proton_candidates[@]} > 0)); then
      info "Detected Proton/Windows-style Hollow Knight install(s):"
      local proton_candidate
      for proton_candidate in "${proton_candidates[@]}"; do
        info "  ${proton_candidate}"
      done
      print_linux_runtime_instructions
    fi
    die "could not auto-discover a valid native Linux Hollow Knight install; pass --game-dir explicitly"
  fi
  if ((${#valid_candidates[@]} > 1)); then
    info "Multiple valid Hollow Knight installs found:"
    local item
    for item in "${valid_candidates[@]}"; do
      info "  ${item%%|*}"
    done
    die "pass --game-dir explicitly"
  fi

  game_dir="${valid_candidates[0]%%|*}"
  local selected_manifest="${valid_candidates[0]#*|}"
  steam_candidate_buildid="$(vdf_value "buildid" "${selected_manifest}")"
  steam_candidate_manifest_ids="$(sed -n 's/^[[:space:]]*"manifest"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "${selected_manifest}" | tr '\n' ' ')"
}

unsupported_steam_version() {
  local version
  local known
  for version in "${KNOWN_UNSUPPORTED_VERSIONS[@]}"; do
    while IFS= read -r known; do
      [[ -n "${known}" ]] || continue
      if [[ "${known}" == "${steam_candidate_buildid}" ]]; then
        printf '%s' "${version}"
        return 0
      fi
    done < <(
      json_array_values_in_unsupported_version "${version}" "knownSteamBuildIds"
      builtin_unsupported_steam_build_ids "${version}"
    )

    while IFS= read -r known; do
      [[ -n "${known}" ]] || continue
      case " ${steam_candidate_manifest_ids} " in
        *" ${known} "*) printf '%s' "${version}"; return 0 ;;
      esac
    done < <(
      json_array_values_in_unsupported_version "${version}" "knownSteamManifestIds"
      builtin_unsupported_steam_manifest_ids "${version}"
    )
  done
  return 1
}

reject_unsupported_steam_version() {
  local version
  version="$(unsupported_steam_version)" || return 0

  info "Steam buildid: ${steam_candidate_buildid:-unknown}"
  [[ -n "${steam_candidate_manifest_ids}" ]] && info "Steam depot manifest IDs: ${steam_candidate_manifest_ids}"
  printf '\nHollow Knight %s is installed, but this release does not support it.\n' "${version}" >&2
  printf 'Downpatch Hollow Knight to one of the supported versions, then re-run this installer.\n\n' >&2
  print_supported_targets
  exit 1
}

infer_target() {
  local targets=()
  local inferred=()
  local target_name
  local known
  mapfile -t targets < <(manifest_targets)

  if ((${#targets[@]} == 1)); then
    target="${targets[0]}"
    info "Inferred target ${target}: release manifest contains exactly one target"
    return 0
  fi

  if [[ -z "${steam_candidate_buildid}${steam_candidate_manifest_ids}" ]]; then
    die "found Hollow Knight at ${game_dir}, but could not infer HKTI target because this release contains multiple targets and no usable Steam build metadata was found; pass --target explicitly, for example --target v1432"
  fi

  reject_unsupported_steam_version

  for target_name in "${targets[@]}"; do
    while IFS= read -r known; do
      [[ -n "${known}" ]] || continue
      if [[ "${known}" == "${steam_candidate_buildid}" ]]; then
        inferred+=("${target_name}")
      fi
    done < <(json_array_values_in_target "${target_name}" "knownSteamBuildIds")

    while IFS= read -r known; do
      [[ -n "${known}" ]] || continue
      case " ${steam_candidate_manifest_ids} " in
        *" ${known} "*) inferred+=("${target_name}") ;;
      esac
    done < <(json_array_values_in_target "${target_name}" "knownSteamManifestIds")
  done

  local unique=()
  local item
  for item in "${inferred[@]}"; do
    case " ${unique[*]} " in
      *" ${item} "*) ;;
      *) unique+=("${item}") ;;
    esac
  done

  if ((${#unique[@]} == 1)); then
    target="${unique[0]}"
    info "Inferred target ${target}: Steam metadata matched release manifest"
    return 0
  fi
  if ((${#unique[@]} > 1)); then
    info "Found Hollow Knight at: ${game_dir}"
    info "Steam buildid: ${steam_candidate_buildid:-unknown}"
    [[ -n "${steam_candidate_manifest_ids}" ]] && info "Steam depot manifest IDs: ${steam_candidate_manifest_ids}"
    die "Steam metadata for ${game_dir} matched multiple HKTI targets; pass --target explicitly, for example --target v1432"
  fi
  info "Steam buildid: ${steam_candidate_buildid:-unknown}"
  [[ -n "${steam_candidate_manifest_ids}" ]] && info "Steam depot manifest IDs: ${steam_candidate_manifest_ids}"
  die "found Hollow Knight at ${game_dir}, but could not infer HKTI target from Steam metadata because this release has no matching build/depot mapping; pass --target explicitly, for example --target v1432"
}

confirm_install() {
  [[ "${assume_yes}" == true ]] && return 0
  printf 'Install target %s into %s? [y/N] ' "${target}" "${game_dir}" >&2
  local reply
  read -r reply
  case "${reply}" in
    y|Y|yes|YES) ;;
    *) die "installation cancelled" ;;
  esac
}

install_payload() {
  local backup_dir=""
  local file
  local src
  local dst
  local copy_files=()

  if [[ "${backup}" == true ]]; then
    backup_dir="${game_dir}/.hkti-backups/$(date -u '+%Y%m%dT%H%M%SZ')-${target}"
    info "Backup directory: ${backup_dir}"
  fi

  for file in "${REQUIRED_PAYLOAD_FILES[@]}"; do
    dst="${game_dir}/${file}"
    if [[ "${file}" == "HollowKnightTasInfo.config" && -f "${dst}" && "${force}" != true ]]; then
      info "Preserving existing HollowKnightTasInfo.config; use --force to overwrite it"
      continue
    fi
    copy_files+=("${file}")
  done

  if [[ "${dry_run}" != true && "${backup}" == true ]]; then
    for file in "${copy_files[@]}"; do
      dst="${game_dir}/${file}"
      if [[ -f "${dst}" ]]; then
        mkdir -p "${backup_dir}/$(dirname -- "${file}")"
        cp -p "${dst}" "${backup_dir}/${file}"
      fi
    done
  fi

  for file in "${copy_files[@]}"; do
    src="${payload_dir}/${file}"
    dst="${game_dir}/${file}"
    if [[ "${dry_run}" == true ]]; then
      if [[ -f "${dst}" && "${backup}" == true ]]; then
        info "Would back up ${dst}"
      fi
      info "Would copy ${src} -> ${dst}"
      continue
    fi
    mkdir -p "$(dirname -- "${dst}")"
    cp -p "${src}" "${dst}"
  done
}

install_steam_appid_file() {
  local appid_file="${game_dir}/steam_appid.txt"
  local backup_dir=""

  if [[ "${backup}" == true ]]; then
    backup_dir="${game_dir}/.hkti-backups/$(date -u '+%Y%m%dT%H%M%SZ')-${target}"
  fi

  if [[ "${dry_run}" == true ]]; then
    if [[ -f "${appid_file}" && "${backup}" == true ]]; then
      info "Would back up ${appid_file}"
    fi
    info "Would write ${APP_ID} -> ${appid_file}"
    return 0
  fi

  if [[ -f "${appid_file}" && "${backup}" == true ]]; then
    mkdir -p "${backup_dir}"
    cp -p "${appid_file}" "${backup_dir}/steam_appid.txt"
  fi
  printf '%s\n' "${APP_ID}" >"${appid_file}"
}

print_libtas_steam_note() {
  info "libTAS Steam note: keep libTAS Virtual Steam client disabled for this setup."
  info "The installer ensures steam_appid.txt contains ${APP_ID} so Hollow Knight can use its bundled Steam API without asking Steam to relaunch outside libTAS."
  info "This avoids the libTAS 1.4.7 Virtual Steam stats shim crash seen during 1432 startup."
}

while (($#)); do
  case "$1" in
    --release)
      [[ $# -ge 2 ]] || die "--release requires a value"
      release_path="$2"
      shift 2
      ;;
    --game-dir)
      [[ $# -ge 2 ]] || die "--game-dir requires a value"
      game_dir="$2"
      shift 2
      ;;
    --steam-root)
      [[ $# -ge 2 ]] || die "--steam-root requires a value"
      steam_roots+=("$2")
      shift 2
      ;;
    --target)
      [[ $# -ge 2 ]] || die "--target requires a value"
      target="$2"
      shift 2
      ;;
    --dry-run)
      dry_run=true
      shift
      ;;
    --yes)
      assume_yes=true
      shift
      ;;
    --backup)
      backup=true
      shift
      ;;
    --no-backup)
      backup=false
      shift
      ;;
    --force)
      force=true
      shift
      ;;
    --verbose)
      verbose=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      die "unknown option: $1"
      ;;
  esac
done

prepare_release_dir "${release_path}"
validate_release_manifest

if [[ -n "${game_dir}" ]]; then
  game_dir="$(absolute_path "${game_dir}")"
else
  discover_game_dir
fi

set +e
validate_game_dir "${game_dir}"
game_dir_status=$?
set -e
if ((game_dir_status != 0)); then
  if ((game_dir_status == 12 || game_dir_status == 14)); then
    print_linux_runtime_instructions
  fi
  die "$(game_dir_error "${game_dir_status}")"
fi

reject_unsupported_steam_version

if [[ -z "${target}" ]]; then
  infer_target
fi

validate_payload "${target}"
verify_checksums

info "Release: ${release_dir}"
info "Payload: ${payload_dir}"
info "Game dir: ${game_dir}"
info "Target: ${target}"
if [[ "${dry_run}" == true ]]; then
  info "Dry run: no files will be modified"
else
  confirm_install
fi

install_payload
install_steam_appid_file
print_libtas_steam_note
info "Install flow complete"
