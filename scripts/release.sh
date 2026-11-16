#!/usr/bin/env bash
# Prepare a release: set the plugin version, build the plugin zip and add the
# version to manifest.json, the Jellyfin plugin repository users subscribe to.
# Called by semantic-release (prepareCmd) with the next version.
# Usage: scripts/release.sh <version>
set -euo pipefail

version="${1:?usage: scripts/release.sh <version>}"
# Jellyfin plugin versions have four parts.
plugin_version="${version}.0"
repository="${GITHUB_REPOSITORY:-Aohzan/jellyfin-plugin-senscritique}"
zip_name="senscritique_${plugin_version}.zip"
timestamp="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
target_abi="$(jq -r .targetAbi meta.json)"

sed -i -E "s#<(Version|AssemblyVersion|FileVersion)>[^<]*<#<\1>${plugin_version}<#" Directory.Build.props
grep -q "<Version>${plugin_version}</Version>" Directory.Build.props || {
	echo "Version not set in Directory.Build.props" >&2
	exit 1
}

jq --arg version "${plugin_version}" --arg timestamp "${timestamp}" \
	'.version = $version | .timestamp = $timestamp' meta.json >meta.json.tmp
mv meta.json.tmp meta.json

# Jellyfin unpacks the zip into plugins/SensCritique_<version>/.
rm -rf dist
dotnet publish Jellyfin.Plugin.SensCritique -c Release -o dist/publish
# zipfile stores the files under their base name, at the root of the zip.
python3 -m zipfile -c "dist/${zip_name}" dist/publish/Jellyfin.Plugin.SensCritique.dll meta.json
checksum="$(md5sum "dist/${zip_name}" | cut -d' ' -f1)"

# Newest version first; a rerun of the same version replaces its entry.
jq --arg version "${plugin_version}" \
	--arg changelog "https://github.com/${repository}/releases/tag/v${version}" \
	--arg targetAbi "${target_abi}" \
	--arg sourceUrl "https://github.com/${repository}/releases/download/v${version}/${zip_name}" \
	--arg checksum "${checksum}" \
	--arg timestamp "${timestamp}" \
	'.[0].versions = [{version: $version, changelog: $changelog, targetAbi: $targetAbi, sourceUrl: $sourceUrl, checksum: $checksum, timestamp: $timestamp}]
		+ [.[0].versions[] | select(.version != $version)]' \
	manifest.json >manifest.json.tmp
mv manifest.json.tmp manifest.json
echo "Built dist/${zip_name} (md5 ${checksum})"
