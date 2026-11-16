# AGENTS.md

Jellyfin 12 plugin that replaces the community rating of movies and series
with their SensCritique rating. It is a remote metadata provider returning only
`CommunityRating` and a `SensCritique` provider id; placed first among a
library's metadata downloaders, its rating wins over TMDb's.

## Layout

| Path | Content |
| --- | --- |
| `Jellyfin.Plugin.SensCritique/Providers/SensCritiqueMetadataProvider.cs` | Provider for `Movie` and `Series`: stored id first, then title search |
| `Jellyfin.Plugin.SensCritique/Matching/TitleMatcher.cs` | Pure matching rules (titles, years), unit tested |
| `Jellyfin.Plugin.SensCritique/Api/SensCritiqueClient.cs` | GraphQL client: search and product by id, throttled |
| `Jellyfin.Plugin.SensCritique/Providers/SensCritiqueExternal*.cs` | Id field in the metadata editor, link to senscritique.com |
| `Jellyfin.Plugin.SensCritique.Tests` | xUnit tests |
| `meta.json` | Plugin manifest shipped in the zip (`targetAbi`, version set at release) |
| `manifest.json` | Jellyfin plugin repository users subscribe to, one entry per release |
| `scripts/release.sh` | Sets the version, builds the zip, adds the release to `manifest.json` |
| `images/logo.svg`, `logo.png` | Catalog image (`imageUrl` of `manifest.json`, 1920x1080) with the SensCritique logo of senscritique.com; PNG rendered from the SVG with `rsvg-convert` |

## Conventions

- Code, comments and commit messages in English.
- Never change the plugin GUID (`Plugin.cs`, `meta.json`, `manifest.json`) or
  the provider name and id key `SensCritique`: installations and the ids
  stored on items depend on them.
- Jellyfin merges remote providers in the library's order and keeps the first
  value of each field (`MetadataService.MergeBaseItemData` with
  `replaceData = false`). Returning anything else than the rating and the id
  would override TMDb's data, so the provider leaves every other field empty.
- An existing `CommunityRating` is only replaced by a refresh with
  `ReplaceAllMetadata`, or by the automatic refresh of the library interval
  (`MetadataRefreshMode.Default` with `requiresRefresh`). A plain refresh keeps
  it: say so when documenting how to apply the plugin to existing items.
- Matching must stay conservative: a wrong rating is worse than TMDb's. A
  change of `TitleMatcher` is checked against a real library, comparing with
  the SensCritique ids Wikidata holds (property P10100, IMDb id P345), and
  keeps zero wrong matches.
- SensCritique's API is undocumented: queries come from the website and other
  clients. Keep requests throttled (`RequestThrottle`) and send no
  introspection or bulk queries.
- `Jellyfin.Controller` and `Jellyfin.Model` are compile-time references
  (`ExcludeAssets runtime`): the server provides them. The CI also builds
  against the oldest Jellyfin that `targetAbi` accepts (`-p:JellyfinVersion`);
  raise `targetAbi` when using an API the oldest version lacks.

## Commands

Tools come from `mise.toml` (no version younger than 7 days).

- `mise run install`, `mise run lint` (prek: dotnet format, dotnet test,
  codespell, yamllint, markdownlint, zizmor), `mise run test`.
- The tests need the ASP.NET Core runtime, which `Jellyfin.Controller`
  references.

## Releases

Conventional Commits are required (checked by `committed`). Merging to `main`
runs semantic-release: `scripts/release.sh` sets the version (`x.y.z.0`) in
`Directory.Build.props` and `meta.json`, builds `senscritique_x.y.z.0.zip`
(DLL and `meta.json`) and adds the release, with the zip's MD5 checksum, to
`manifest.json`. The release commit carries those files and `ChangeLog.md`,
the GitHub release the zip. Jellyfin servers subscribed to `manifest.json`
offer the update.
