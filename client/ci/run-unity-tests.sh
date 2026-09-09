#!/usr/bin/env bash
set -euo pipefail

mode="${1:?Test mode is required}"
assembly="${2:?Test assembly is required}"
case "$mode:$assembly" in
  editmode:Baryonyx.EditModeTests|playmode:Baryonyx.PlayModeTests) ;;
  *) echo 'Unsupported test mode or assembly.' >&2; exit 1 ;;
esac

version=v0.1.55
sha256=2c8b84640377f3cfbccd1549df4723a111ba8016d7891b591a61444789cb3ecc
cli_directory="${RUNNER_TEMP:?RUNNER_TEMP is required}/baryonyx-game-ci"
cli="$cli_directory/game-ci"
mkdir -p "$cli_directory"
if [ ! -f "$cli" ]; then
  curl --fail --location --retry 3 \
    "https://github.com/game-ci/cli/releases/download/$version/game-ci-linux-x64" \
    --output "$cli.download"
  printf '%s  %s\n' "$sha256" "$cli.download" | sha256sum --check
  mv -- "$cli.download" "$cli"
fi
# Verify cached binaries too, before executing them.
printf '%s  %s\n' "$sha256" "$cli" | sha256sum --check
chmod +x "$cli"

# The Action wrapper emits --no-coverageEnabled, which this CLI rejects.
# Use the explicit boolean value and the registered ULF licensing method.
exec "$cli" test --docker --engine=unity client \
  --testPlatforms="$mode" \
  --dockerShmSize=1025m \
  --coverageEnabled=false \
  --unityLicensingMethod=file \
  --customImage="unityci/editor:${UNITY_VERSION:?UNITY_VERSION is required}-linux-il2cpp-3" \
  --customParameters="-assemblyNames $assembly" \
  --artifactsPath="client/TestResults/$mode"
