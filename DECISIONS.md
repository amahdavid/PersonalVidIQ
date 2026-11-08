# PersonalVidIQ: decisions before Milestone 0

Settled 2026-11-07. The source brief is the Notion page "Personal vidIQ Alternative — Implementation Brief" (`411bce3163cf81738d0bc965debedf67`). Where this file differs from the brief, this file wins. The Notion page has not been edited yet.

## Decided with the user

| # | Topic | Decision | Changes to the brief |
|---|---|---|---|
| 1 | MCP client | **Claude only, local only** (Claude Desktop / Claude Code), using a **local stdio** MCP server. No tunnel, no remote HTTPS, no OAuth, nothing online. ChatGPT wrote the brief, so read every "ChatGPT" in it as "Claude". In the user's words: "im building it for claude ... it's for claude on th local and not online". | Brief said "ChatGPT through MCP"; Milestone 4's "verify from ChatGPT" now means verifying from Claude. |
| 2 | Data retention | **Short-lived snapshots.** Store tracked IDs and settings indefinitely. View, like, comment and subscriber snapshots are **deleted automatically after 30 days** (configurable, max 30). Analysis windows (`periodDays`) are capped at 30. | Replaces "add retention policies only after the data volume is understood". |
| 3 | Tool list | **Add** `track_channel`, `untrack_channel`, `track_video`, `untrack_video`, `list_tracked`, plus `refresh_tracked` (see #6). **Drop** `ResearchRun` and `get_research_report` from the MVP. | Fixes the definition-of-done gap. |
| 4 | Tracking scope | Tracking a channel snapshots its **last 50 uploads right away** (an instant outlier baseline) and **every new upload** after that. | New. |
| 5 | Database | **PostgreSQL via Homebrew** (`postgresql@17` as a brew service). No Docker. | Replaces the Docker Compose item in Milestone 0. |
| 6 | Collector | **Only when asked**, with no scheduler. Snapshots are taken by a `refresh_tracked` MCP tool and a `collect` CLI command. The due rules still apply (see "Defaults"), so repeat calls don't waste quota. A launchd job can be added later through config if gaps get annoying. | Replaces the always-on `BackgroundService` for the MVP. |
| 7 | Installs | Claude may install the current .NET LTS SDK and Postgres with Homebrew **when Milestone 0 starts**. | — |
| 8 | Search quota | `search_youtube` gets a **cap of 50 calls a day** (5,000 units, configurable) and a **24 h cache** per normalised query. Each response reports units used and units left today. | New. |
| 9 | Shorts vs long-form | **A video of 180 s or less counts as a Short** (configurable). Outlier cohorts never mix Shorts and long-form. | New. |
| 10 | Keyword suggestions | **No YouTube autocomplete.** Keyword ideas (Milestone 6) come from the titles and tags of official search results and tracked channels, labelled with that source. | Removes the brief's autocomplete source. |
| 11 | First channels | No list yet. The user adds channels with `track_channel` once it works. | Answers the brief's open question for now. |

## Defaults set without asking (change any you disagree with)

- **Policy basis.** YouTube API Developer Policies III.E.4.b and d: statistics fetched with an API key ("non-authorized data") must not be stored for more than 30 days, and after 30 calendar days they must be deleted or refreshed. III.E.4.h: derived metrics need a clear disclosure that they are not YouTube's figures. So every calculated metric carries `source: "calculated by PersonalVidIQ (not a YouTube metric)"`.
- **Metadata refresh.** Titles, descriptions, durations and thumbnails of tracked videos are refreshed on each collect. Metadata for anything untracked and not refreshed within 30 days is purged. Search-cache rows expire after 24 h.
- **Velocity uses real elapsed time** between two snapshots, never the expected interval. Gaps are normal because collection only runs when asked. The output always includes the snapshot timestamps and the hours between them.
- **When a snapshot is due**: a video younger than 48 h gets one every 3 h, a video from 2 to 30 days old once a day, and a video over 30 days old once a week, or never if it isn't in a cohort being analysed. Channel stats are snapshotted once a day.
- **Outliers before any history exists.** `find_outliers` falls back to **lifetime views per day since publish**, compared with the median of the same channel's videos in the same format (Short or long-form) and a similar age band. Output is labelled `method: lifetime_average`. Once 2 or more snapshots exist, it switches to recent velocity (`method: snapshot_velocity`). If the cohort has fewer than 5 videos or a median of zero, the score is unavailable or low confidence.
- **Channel input.** Tools accept a channel ID (`UC…`), an `@handle` or a channel URL. Handles are resolved with `channels.list?forHandle=` (1 unit). Legacy `/c/` and `/user/` URLs are resolved where the API supports it, and an error is returned otherwise.
- **Channel videos** come from the uploads playlist via `playlistItems.list` (1 unit), never from `search.list`. "Popular" ordering is a local sort of the fetched set, labelled as such.
- **Subscriber counts** are rounded by YouTube to 3 significant figures, so subscriber growth is always labelled low confidence. Velocity and outliers rest on views.
- **Shape of the stdio host**: one console app, `PersonalVidIQ.Host` (replaces the brief's `PersonalVidIQ.Api`), built as `personalvidiq` with the generic host plus the MCP C# SDK stdio transport. Commands: `mcp` (Claude launches it), `status`, and `collect` (added with Milestone 5). It needs no ASP.NET Core web host, because nothing is served over HTTP. Logs go to stderr only, never stdout, because stdio carries the protocol.
- **Milestone 0 stack (2026-11-08)**: .NET 10.0.403 SDK (LTS), ModelContextProtocol 2.4.0, EF Core / Npgsql 10, xUnit v3 on Microsoft.Testing.Platform, central package versions in `Directory.Packages.props`. Postgres 17 runs as a Homebrew login service; databases `personalvidiq` and `personalvidiq_test`.

## Out of scope: ChatGPT and remote access

There's no ChatGPT milestone. For reference only, from OpenAI's Apps SDK auth docs (checked 2026-11-07): ChatGPT would need a public HTTPS tunnel and OAuth 2.1, and it can't send API keys. Because the tool is local-only, the brief's items on remote exposure (remote auth and secure remote deployment in Milestone 4) don't apply.

## Still open (none block Milestone 0)

- Whether to add a launchd schedule once on-demand collection has been tried.
- Which channels to track first.
- Whether to connect the user's own channel through OAuth or the Analytics API. Authorized data can be kept longer and includes CTR and retention.
- Whether to update the Notion brief to match this file. Ask before editing.
