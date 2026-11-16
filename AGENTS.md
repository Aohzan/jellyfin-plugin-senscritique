# AGENTS.md

Jellyfin 12 plugin that replaces the community rating of movies and series
with their SensCritique rating. It is a custom metadata provider
(`ICustomMetadataProvider`): Jellyfin runs it after the metadata downloaders,
on the identified item, and it sets `CommunityRating` and a `SensCritique`
provider id.

## Layout

| Path | Content |
| --- | --- |
| `Jellyfin.Plugin.SensCritique/Providers/SensCritiqueMetadataProvider.cs` | Custom provider for `Movie` and `Series`: stored id first, then title search; sets the rating and the id on the item |
| `Jellyfin.Plugin.SensCritique/Matching/TitleMatcher.cs` | Pure matching rules (titles, years), unit tested |
| `Jellyfin.Plugin.SensCritique/Api/SensCritiqueClient.cs` | GraphQL client: search and product by id, throttled |
| `Jellyfin.Plugin.SensCritique/Providers/SensCritiqueExternal*.cs` | Id field in the metadata editor, link to senscritique.com |
| `Jellyfin.Plugin.SensCritique.Tests` | xUnit tests |
| `meta.json` | Plugin manifest shipped in the zip (`targetAbi`, version set at release) |
| `manifest.json` | Jellyfin plugin repository users subscribe to, one entry per release |
| `scripts/release.sh` | Sets the version, builds the zip, adds the release to `manifest.json` |
| `css/rating-icon.css` | Custom CSS for the web client: SensCritique symbol (extracted from the logo, base64 SVG mask) instead of the rating star, for both the legacy (`span.starIcon`) and React (`svg`) markups |
| `images/logo.svg`, `logo.png` | Catalog image (`imageUrl` of `manifest.json`, 1920x1080) with the SensCritique logo of senscritique.com; PNG rendered from the SVG with `rsvg-convert` |

## Conventions

- Code, comments and commit messages in English.
- Never change the plugin GUID (`Plugin.cs`, `meta.json`, `manifest.json`) or
  the provider name and id key `SensCritique`: installations and the ids
  stored on items depend on them.
- Why a custom provider and not a remote one (metadata downloader): a remote
  provider runs before TMDb has identified a new item, so it would search with
  the file name; and an existing `CommunityRating` is only replaced by a
  refresh with `ReplaceAllMetadata` or by the library's automatic refresh.
  `MetadataService` runs custom providers after merging the downloaders' data
  into the item, whenever it runs all providers (new item, `FullRefresh`,
  `ReplaceAllMetadata`, automatic refresh), and saves what they change.
- Custom providers are not listed among a library's metadata downloaders: they
  cannot be enabled per library and always run on movies and series.
- `ItemUpdateType.None` is a flag (1), not 0: return it or `MetadataDownload`,
  never a combination.
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
