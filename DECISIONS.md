# PersonalVidIQ: decisions before Milestone 0

Settled 2026-11-07. The source brief is the Notion page "Personal vidIQ Alternative — Implementation Brief" (`411bce3163cf81738d0bc965debedf67`). Where this file differs from the brief, this file wins. The Notion page has not been edited yet.

**Purpose (2026-11-08):** a general-purpose tool for any YouTube channel and niche, not just one channel. The goal is reliable, clearly sourced data for making decisions about a channel. Nothing is tuned to one genre.

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

## Milestone 1 notes (2026-11-08)

- **Quota day**: counted midnight to midnight Pacific Time, like YouTube. Failed calls and retries are counted too, because YouTube charges for them. Quota costs per endpoint are configurable (`YouTube:QuotaCosts`).
- **Per-process for now**: the quota ledger and the 24 h search cache live in memory, so they reset when Claude restarts the server. Milestone 2 moves both into PostgreSQL, so the MCP server and the CLI share one count.
- **Local guard**: a call that would push the day's count over `DailyQuotaUnits` is refused before it's sent.
- **Retries**: up to 3, with exponential backoff, for network errors, 5xx and rate limits only. Quota, key and not-found errors fail at once, with a plain message that never includes the key.
- **Legacy `/c/` URLs** can't be resolved through the API, so the error asks for the @handle or the `/channel/UC…` URL instead. `/user/` URLs use `forUsername`.
- **Search's `videoDuration=short` filter** means "under 4 minutes" to YouTube, not "is a Short". Format is still decided by the 180 s rule.

## Milestone 2 notes (2026-11-08)

- **Tables**: `channels`, `videos`, `channel_snapshots`, `video_snapshots`, `quota_usage`, `search_cache`.
  - The brief's `TrackedChannel` is now `Channel` with an `IsTracked` flag, because stored videos can belong to channels you don't track.
  - `ResearchRun` was dropped (#3).
- **Tracking source**: a video is tracked either `Manual` (track_video) or `Channel` (through its channel). The first source sticks. Untracking a channel only untracks the videos it brought in, so manually tracked ones stay.
- **Idempotent snapshots**: a snapshot is skipped if the same video or channel already has one from less than `MinSnapshotSpacingMinutes` ago (default 15). There's also a unique index on (item, captured time).
- **Retention purge** runs when the MCP server starts and with `personalvidiq purge`. It deletes:
  - snapshots older than `SnapshotRetentionDays`;
  - untracked videos and channels not refreshed for `StaleMetadataDays` (30), with channels only removed once they have no videos;
  - expired search cache rows;
  - quota rows older than 7 days.
  
  Tracked items are never deleted. Keeping them fresh is the collector's job (Milestone 5).
- **Quota ledger and search cache now live in Postgres**, shared by the MCP server and the CLI. Quota counts use an atomic upsert, so concurrent calls never lose a count.
- **Migrations** are applied automatically at startup (`Database:AutoMigrate`, default on), or with `personalvidiq migrate`.

## Milestone 3 notes (2026-11-08)

- **View velocity** = (latest views − earlier views) ÷ real days between them.
  - It needs 2 or more counts taken at different times.
  - With a window (24 h or 7 d), the starting point is the newest snapshot at least that old. If the history is shorter, the output says so.
  - A falling count gets low confidence and a note.
  - Confidence by time span: under 1 h low, 1–6 h medium, 6 h or more high.
- **Lifetime views/day** = views ÷ days since publish, treating any video as at least 1 hour old. Low confidence under a day old, otherwise medium.
- **Outlier score** = the video's views/day ÷ the median views/day of the same channel's videos in the same format (Short or long-form) and of a similar age.
  - Similar age means 0.5×–2× the video's age, widened to 0.25×–4× if fewer than 5 match. Ages under 1 day count as 1 day when matching.
  - It uses recent snapshot velocity (7-day window, at least 12 h of history) when the video and 5 or more of its comparison group have it, and lifetime views/day otherwise. The output names the method used.
  - Bands: 3× or more well above, 1.5× or more above, above 0.67× typical, otherwise below.
  - It's unavailable for fewer than 5 comparison videos, a median of zero, an unknown duration, or live and upcoming videos.
  - Confidence is high with 10 or more comparison videos and no widening, otherwise medium. It's low when the video is under a day old and the lifetime method is used.
- **Engagement rates**: likes ÷ views and comments ÷ views, as percentages. Low confidence under 1,000 views.
- **Channel comparison**:
  - The period is 1–30 days, capped by the retention window. Each channel's latest 50 uploads are checked.
  - Reported: uploads per week, Shorts vs long-form counts, and median lifetime views/day per format.
  - From snapshots: growth in total views/day and subscriber change. Subscriber change is always low confidence because of rounding.
- **Free history**: whenever analysis fetches fresh stats for a video or channel that's already stored, that read is saved as a snapshot.
- **Baseline for outliers**: find_outliers uses a channel's latest 50 uploads by default (10–200), and costs about 3 quota units. analyze_video costs about 4.

## Milestone 4 notes (2026-11-08)

- **14 tools** (quota cost in brackets):
  - `get_status` (0)
  - `search_youtube` (100 + 1, capped and cached)
  - `get_channel` (1)
  - `get_channel_videos` (about 3; up to 9 for popular or a format filter)
  - `get_video` (1)
  - `analyze_video` (about 4)
  - `compare_videos` (1, up to 10 videos)
  - `compare_channels` (about 3 per channel, up to 5)
  - `find_outliers` (about 3)
  - `track_channel` (about 3)
  - `untrack_channel` (0)
  - `track_video` (1–2)
  - `untrack_video` (0)
  - `list_tracked` (0)
- **Added beyond the brief**: `compare_videos` and `get_status`.
- **Moved**: `refresh_tracked` lands in Milestone 5 with the collector.
- **Search results** are enriched with stats from videos.list (1 unit), so each hit shows views, format and lifetime views/day. Notes say plainly that ranking isn't search volume.
- **"Popular" channel videos** means the most viewed among the latest 200 uploads. It's a local sort, labelled as such. YouTube's own popularity sort is search-only and costs 100 units.
- **Errors** come back to Claude as tool errors with a plain label: Setup needed, API key problem, Quota used up, Search cap reached, Not found, Invalid input, or YouTube temporarily unavailable.
- **Connected to Claude Code** at user scope through the published copy in `~/.local/share/personalvidiq`. Also connected to Claude Desktop chats through `claude_desktop_config.json` (same published copy; restart Desktop after re-publishing).
- **Source on GitHub, public** (2026-11-08): https://github.com/amahdavid/PersonalVidIQ. The user chose public ("make it public, it's fine"). Secrets stay in .NET user secrets and are never committed; the history was checked for the API key before the first push.

## Milestone 5 notes (2026-11-08)

- **Refreshing** is on demand, with no scheduler (#6): run the `refresh_tracked` tool or `personalvidiq collect`.
  - Each refresh first runs the retention purge, then collects only what's due.
  - Due rules:
    - Videos under 48 h old: every 3 h.
    - Up to 30 days old: daily.
    - Older: weekly, which also keeps metadata inside the 30-day refresh rule.
    - Channel stats: daily.
    - Each tracked channel's uploads playlist (latest 20): every 3 h.
  - An item counts as due once 90% of its interval has passed. Time is measured as real elapsed time, so laptop sleep only delays things.
- **New uploads**: any video id in a tracked channel's latest uploads that PersonalVidIQ has never stored gets tracked automatically. Videos you untracked stay untracked, because they're still stored.
- **Deleted or private videos** that YouTube no longer returns are untracked and listed in the report.
- **Quota budget**: a refresh leaves `QuotaReserveUnits` (1,000) for searches and lookups. If the budget is short, the channel work is skipped and the oldest-tier videos are dropped first. Whatever's skipped stays due.
- **`force`** refreshes everything now, but the 15-minute snapshot spacing still applies. **`dryRun`** shows what's due and the estimated units without fetching anything.
- **Run log**: each refresh is recorded in `collection_runs` (status, counts, units, error) and kept for 7 days. `get_status` shows the last run, what's due now and when the next item comes due.
- **Typical cost**: about 1 unit per 50 due videos, plus 1 per tracked channel's uploads check.

## Milestone 6 notes (2026-11-08)

- **Five tools.** No new tables or stored keyword data: everything is worked out at request time from stored videos, which already follow the 30-day rule.
  - `research_keyword` (about 102 units, search cached for 24 h): competition stats on the top results.
    - Stats: median views and views/day, the share published in the last 30 days, the share that are Shorts, the share with the keyword in the title, distinct channels, median channel subscribers, and the share from channels under 10k subscribers (`Research:SmallChannelSubscribers`).
    - Also lists related terms from result titles and tags. YouTube's total-results figure is labelled as a rough index count.
  - `get_keyword_ideas` (0 units): keywords from tracked channels' stored titles and tags, ranked by median channel-relative ratio. Takes an optional seed and groups keywords into topics.
  - `discover_trends` (0 units): keywords emerging across tracked channels, comparing a recent window of 1–15 days with the window before it.
  - `get_recent_uploads` (0 units): competitor upload monitoring.
  - `get_video_idea_evidence` (0 units, or about 2 if your channel isn't stored): recent outliers, best keywords, emerging keywords, and keyword gaps against your channel.
- **Keywords** are single words and two-word phrases from titles, plus tags of 1–4 words.
  - Tokenising works on Unicode letters and digits, so any language is handled.
  - Built-in stop words are English function words and YouTube boilerplate only, and you can add more in config. Nothing niche-specific is built in.
  - Words that only ever appear inside one phrase are folded into that phrase.
- **Keyword score** = the median channel-relative ratio of videos that use the keyword (1.0 = typical for that channel, format and age).
  - It's labelled as a correlation, and confidence tops out at medium: 8 or more scored videos from 2 or more channels is medium, 3 or more is low.
  - A keyword is reported only once it's in at least 3 videos (`Research:MinVideosPerTerm`).
- **Topics**: keywords whose video sets overlap by at least 0.5 (Jaccard) are grouped. Each topic is labelled with its most specific phrase that's in at least half of the topic's videos.
- **Emerging keywords** need 3 or more recent videos, and 2 or more channels when at least 2 are tracked. The keyword must then be used at least twice as often as in the previous window, or its recent videos must have a median ratio of 1.5 or more, or both.
  - It's never called a trend on the strength of one video, and it's never a claim about YouTube-wide demand.
  - Confidence is medium with 3 or more channels and 5 or more scored videos, otherwise low.
- **Video ideas**: the app returns evidence and how to use it. Claude writes the ideas and cites the evidence, which keeps the app at $0 with no LLM API.
- **Not done**: no autocomplete (#10), no search volume, and no paid keyword provider. A paid provider is worth looking at only if the free signals prove too thin in real use (open question).

## Out of scope: ChatGPT and remote access

There's no ChatGPT milestone. For reference only, from OpenAI's Apps SDK auth docs (checked 2026-11-07): ChatGPT would need a public HTTPS tunnel and OAuth 2.1, and it can't send API keys. Because the tool is local-only, the brief's items on remote exposure (remote auth and secure remote deployment in Milestone 4) don't apply.

## Still open (none block Milestone 0)

- Whether to add a launchd schedule once on-demand collection has been tried.
- Which channels to track first.
- Whether to connect the user's own channel through OAuth or the Analytics API. Authorized data can be kept longer and includes CTR and retention.
- Whether to update the Notion brief to match this file. Ask before editing.
