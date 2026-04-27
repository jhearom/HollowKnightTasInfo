#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
readonly PROJECT_FILE="${REPO_ROOT}/Assembly-CSharp.TasInfo.mm.csproj"
readonly RELEASE_ROOT="${REPO_ROOT}/bin/HK TAS Info Tool"
readonly DEFAULT_CONFIGS=("v1028" "v1028_Krythom" "v1221" "v1432")
readonly REQUIRED_LIB_FILES=("Assembly-CSharp.dll" "PlayMaker.dll" "UnityEngine.dll" "zlib.net.dll")
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

configs=()
restore=true
verbose=false
dotnet_cmd="${DOTNET:-dotnet}"

usage() {
  cat <<'EOF'
Usage: scripts/build-release.sh [options]

Build and validate HollowKnightTasInfo release payloads.

Options:
  --all                  Build all supported targets (default).
  --config NAME          Build one target. May be passed more than once.
  --configs LIST         Comma-separated target list, or "all".
  --skip-restore         Skip the initial dotnet restore.
  --dotnet PATH          dotnet command/path to use.
  --verbose              Use normal dotnet build verbosity instead of minimal.
  -h, --help             Show this help.

Supported targets:
  v1028, v1028_Krythom, v1221, v1432

Environment:
  DOTNET_CLI_HOME and NUGET_PACKAGES are honored if already set.
  DOTNET may be used instead of --dotnet.
EOF
}

die() {
  printf 'error: %s\n' "$*" >&2
  exit 1
}

info() {
  printf '[build-release] %s\n' "$*"
}

contains_config() {
  local needle="$1"
  local config
  for config in "${DEFAULT_CONFIGS[@]}"; do
    [[ "${config}" == "${needle}" ]] && return 0
  done
  return 1
}

add_config() {
  local config="$1"
  contains_config "${config}" || die "unsupported configuration: ${config}"
  local existing
  for existing in "${configs[@]}"; do
    [[ "${existing}" == "${config}" ]] && return 0
  done
  configs+=("${config}")
}

add_config_list() {
  local list="$1"
  local config
  if [[ "${list}" == "all" ]]; then
    configs=("${DEFAULT_CONFIGS[@]}")
    return 0
  fi
  IFS=',' read -r -a parsed_configs <<<"${list}"
  for config in "${parsed_configs[@]}"; do
    [[ -n "${config}" ]] || die "empty configuration in --configs ${list}"
    add_config "${config}"
  done
}

project_version() {
  sed -n 's:.*<AssemblyVersion>\(.*\)</AssemblyVersion>.*:\1:p' "${PROJECT_FILE}" | head -n 1
}

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\n'/\\n}"
  printf '%s' "${value}"
}

sha256_file() {
  sha256sum "$1" | awk '{print $1}'
}

patch_label() {
  case "$1" in
    v1028) printf '1.0.2.8' ;;
    v1028_Krythom) printf '1.0.2.8 Krythom' ;;
    v1221) printf '1.2.2.1' ;;
    v1432) printf '1.4.3.2' ;;
    *) printf '%s' "$1" ;;
  esac
}

known_steam_build_ids_json() {
  case "$1" in
    v1432) printf '["4296065"]' ;;
    *) printf '[]' ;;
  esac
}

known_steam_manifest_ids_json() {
  case "$1" in
    v1432) printf '["4631363637747835650"]' ;;
    *) printf '[]' ;;
  esac
}

validate_lib_inputs() {
  local config="$1"
  local file
  for file in "${REQUIRED_LIB_FILES[@]}"; do
    [[ -f "${REPO_ROOT}/lib/${config}/${file}" ]] || die "missing lib/${config}/${file}"
  done
  if [[ "${config}" == "v1432" ]]; then
    [[ -f "${REPO_ROOT}/lib/v1432/UnityEngine.CoreModule.dll" ]] || die "missing lib/v1432/UnityEngine.CoreModule.dll"
    [[ -f "${REPO_ROOT}/lib/v1432/UnityEngine.Physics2DModule.dll" ]] || die "missing lib/v1432/UnityEngine.Physics2DModule.dll"
  fi
}

validate_payload() {
  local config="$1"
  local payload_dir="${RELEASE_ROOT}/${config}"
  local file
  [[ -d "${payload_dir}" ]] || die "missing release payload directory: ${payload_dir}"
  for file in "${REQUIRED_PAYLOAD_FILES[@]}"; do
    [[ -f "${payload_dir}/${file}" ]] || die "missing ${config} payload file: ${file}"
  done
}

write_checksums() {
  local checksum_file="${RELEASE_ROOT}/SHA256SUMS"
  info "Writing SHA256SUMS"
  (
    cd "${RELEASE_ROOT}"
    find . -type f \
      ! -name 'SHA256SUMS' \
      ! -name 'release-manifest.json' \
      -print0 |
      sort -z |
      xargs -0 sha256sum
  ) >"${checksum_file}"
}

copy_release_scripts() {
  info "Copying release installer script"
  mkdir -p "${RELEASE_ROOT}/scripts"
  cp -p "${REPO_ROOT}/scripts/install-linux.sh" "${RELEASE_ROOT}/scripts/install-linux.sh"
}

write_manifest() {
  local version="$1"
  local manifest_file="${RELEASE_ROOT}/release-manifest.json"
  local generated_at
  generated_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"

  info "Writing release-manifest.json"
  {
    printf '{\n'
    printf '  "toolVersion": "%s",\n' "$(json_escape "${version}")"
    printf '  "generatedAtUtc": "%s",\n' "$(json_escape "${generated_at}")"
    printf '  "steamAppId": 367520,\n'
    printf '  "supportedRuntime": "native-linux",\n'
    printf '  "recommendedSteamRuntime": "Steam Linux Runtime 1.0 / scout",\n'
    printf '  "targets": {\n'

    local index=0
    local config
    for config in "${configs[@]}"; do
      local payload_dir="${RELEASE_ROOT}/${config}"
      local patched_hash
      local original_hash
      patched_hash="$(sha256_file "${payload_dir}/hollow_knight_Data/Managed/Assembly-CSharp.dll")"
      original_hash="$(sha256_file "${payload_dir}/hollow_knight_Data/Managed/original/Assembly-CSharp.dll")"

      if (( index > 0 )); then
        printf ',\n'
      fi
      printf '    "%s": {\n' "$(json_escape "${config}")"
      printf '      "patchLabel": "%s",\n' "$(json_escape "$(patch_label "${config}")")"
      printf '      "steamAppId": 367520,\n'
      printf '      "steamLinuxDepotId": 367523,\n'
      printf '      "knownSteamBuildIds": %s,\n' "$(known_steam_build_ids_json "${config}")"
      printf '      "knownSteamManifestIds": %s,\n' "$(known_steam_manifest_ids_json "${config}")"
      printf '      "expectedNativeExecutable": "hollow_knight.x86_64",\n'
      printf '      "payloadDirectory": "%s",\n' "$(json_escape "${config}")"
      printf '      "patchedAssemblySha256": "%s",\n' "${patched_hash}"
      printf '      "originalAssemblySha256": "%s"\n' "${original_hash}"
      printf '    }'
      index=$((index + 1))
    done

    printf '\n  }\n'
    printf '}\n'
  } >"${manifest_file}"
}

clean_release_outputs() {
  local version="$1"
  local zip_file="${REPO_ROOT}/bin/HK_TAS_Info_Tool_v${version}.zip"
  info "Cleaning generated release outputs"
  rm -rf "${RELEASE_ROOT}" "${zip_file}"
  mkdir -p "${RELEASE_ROOT}"
}

refresh_zip() {
  local version="$1"
  local zip_file="${REPO_ROOT}/bin/HK_TAS_Info_Tool_v${version}.zip"
  info "Refreshing ${zip_file}"
  rm -f "${zip_file}"
  (
    cd "${RELEASE_ROOT}"
    zip -qr "${zip_file}" .
  )
}

while (($#)); do
  case "$1" in
    --all)
      configs=("${DEFAULT_CONFIGS[@]}")
      shift
      ;;
    --config)
      [[ $# -ge 2 ]] || die "--config requires a value"
      add_config "$2"
      shift 2
      ;;
    --configs)
      [[ $# -ge 2 ]] || die "--configs requires a value"
      add_config_list "$2"
      shift 2
      ;;
    --skip-restore)
      restore=false
      shift
      ;;
    --dotnet)
      [[ $# -ge 2 ]] || die "--dotnet requires a value"
      dotnet_cmd="$2"
      shift 2
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

if ((${#configs[@]} == 0)); then
  configs=("${DEFAULT_CONFIGS[@]}")
fi

version="$(project_version)"
[[ -n "${version}" ]] || die "could not read AssemblyVersion from ${PROJECT_FILE}"

info "Release version: ${version}"
info "Configurations: ${configs[*]}"

for config in "${configs[@]}"; do
  validate_lib_inputs "${config}"
done

clean_release_outputs "${version}"

if [[ "${restore}" == true ]]; then
  info "Restoring dependencies"
  "${dotnet_cmd}" restore "${PROJECT_FILE}" -v minimal
fi

build_verbosity="minimal"
if [[ "${verbose}" == true ]]; then
  build_verbosity="normal"
fi

for config in "${configs[@]}"; do
  info "Building ${config}"
  PATH="${REPO_ROOT}/bin/${config}/net35:${PATH}" "${dotnet_cmd}" build "${PROJECT_FILE}" \
    -c "${config}" \
    --no-restore \
    -p:SolutionDir="${REPO_ROOT}/" \
    -v "${build_verbosity}"
  validate_payload "${config}"
done

write_manifest "${version}"
copy_release_scripts
write_checksums
refresh_zip "${version}"

info "Validated release payload: ${RELEASE_ROOT}"
info "Release archive: ${REPO_ROOT}/bin/HK_TAS_Info_Tool_v${version}.zip"
