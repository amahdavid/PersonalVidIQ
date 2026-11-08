# PersonalVidIQ

A private, local-only YouTube research tool for **any channel in any niche**, whether Shorts, long-form or both. Its job is to give the user **reliable, clearly sourced data so they can make informed decisions about their channel**. Claude (Desktop/Code) is the only client, through a **local stdio MCP server**. Nothing runs online: no HTTP endpoint, tunnel, OAuth or ChatGPT.

Reliability comes before cleverness. Every number says where it came from, how it was worked out, and how far to trust it. When the data can't support an answer, the tool says "unavailable" and why. Never hard-code anything niche-specific (topics, keywords, channel lists, thresholds tuned to one genre); keep it configurable. Test fixtures happen to use football sample data, and that's all it is.

## Sources of truth

1. **[DECISIONS.md](DECISIONS.md)**: every settled decision. Read it before changing scope or behaviour.
2. The Notion brief "Personal vidIQ Alternative — Implementation Brief" (`411bce3163cf81738d0bc965debedf67`). ChatGPT wrote it, so read every "ChatGPT" in it as "Claude". Where it disagrees with DECISIONS.md, DECISIONS.md wins. Don't edit Notion without asking.
3. [README.md](README.md): setup, and how to connect Claude.

When a new decision is made with the user, add it to DECISIONS.md in the same change.

## Status

- [x] Milestone 0: solution, stdio host, `get_status` tool, Postgres, tests
- [x] Milestone 1: YouTube Data API client (search, channels, videos, uploads playlist, quota ledger, search cap and cache, retries)
- [x] Milestone 2: EF Core entities, migrations, stores, idempotent snapshots, 30-day purge, Postgres quota ledger and search cache
- [x] Milestone 3: analytics (velocity, lifetime views/day, engagement rates, outlier scoring, video/channel comparison services)
- [x] Milestone 4: 14 MCP tools (search, channel/video lookups, analysis, outliers, comparisons, tracking, status); registered with Claude Code
- [ ] Milestone 5: `refresh_tracked` tool + `collect` command (on demand, no scheduler, due rules)
- [ ] Milestone 6: research features (no autocomplete)

Update this list when a milestone lands.

## Commands

```bash
dotnet build                                          # warnings are errors
dotnet test                                           # Microsoft.Testing.Platform (set in global.json)
dotnet run --project src/PersonalVidIQ.Host -- status # setup/health report
dotnet run --project src/PersonalVidIQ.Host -- migrate
dotnet run --project src/PersonalVidIQ.Host -- purge
dotnet ef migrations add <Name> --project src/PersonalVidIQ.Infrastructure --output-dir Persistence/Migrations
```

The `mcp` command applies pending migrations and runs the purge at startup (`Database:AutoMigrate`). A test fails if the model changes without a migration.

Integration tests need local Postgres (`brew services start postgresql@17`) and the `personalvidiq_test` database. The live YouTube test is skipped unless `PERSONALVIDIQ_YOUTUBE_API_KEY` is set (about 3 units, no search).

## Rules that are easy to break

- **stdout belongs to the MCP protocol.** Never `Console.WriteLine` in the `mcp` path. Logs go to stderr only.
- **30-day limit:** YouTube API Developer Policies III.E.4 say non-authorized statistics (views, likes, subscribers) may not be stored for more than 30 days. Snapshots get purged, and `SnapshotRetentionDays` is capped at 30. Don't add long-term history.
- **Label every metric** with `MetricSource`: YouTube API, calculated by PersonalVidIQ, third-party estimate, or unavailable. Calculated numbers must say they're not YouTube metrics (policy III.E.4.h). Never invent search volume, CTR, impressions, retention or history that wasn't collected.
- **Quota:** `search.list` costs 100 units, while `videos.list`, `channels.list` and `playlistItems.list` cost 1. Channel videos come from the uploads playlist, never search. Batch video IDs 50 at a time. Search has a daily cap and a 24 h cache.
- **Shorts are 180 s or less** (configurable). Never mix Shorts and long-form in one outlier cohort.
- **No scraping** and no undocumented endpoints. That includes YouTube autocomplete.
- **$0/month.** No paid APIs or services, and no OpenAI or Anthropic API calls from the app. Ask before adding any dependency that costs money, and before installing software.
- **Keep secrets out of the repo.** The API key lives in .NET user secrets (`YouTube:ApiKey`). Never log keys or connection strings.

## MCP tools

`get_status`, `search_youtube`, `get_channel`, `get_channel_videos`, `get_video`, `analyze_video`, `compare_videos`, `compare_channels`, `find_outliers`, `track_channel`, `untrack_channel`, `track_video`, `untrack_video`, `list_tracked`.

- **Tool classes** (`src/PersonalVidIQ.Mcp/Tools`) only parse parameters and call a service through `ToolGuard.Run`, which turns `YouTubeApiException` into a readable `McpException`.
- **Enums** serialize as snake_case strings.
- **Every tool description** states its quota cost.
- **Integration tests** start the real server over stdio, pointed at a closed port so they never reach YouTube. The exception is the key-gated live round trip.

## Architecture

The layers are Domain → Application → Infrastructure, with Mcp and Host on top.

- **Analytics** are pure, deterministic functions in `Application/Analytics`, with unit tests. Every calculated number is a `CalculatedMetric` carrying its formula, window, sample size, confidence and notes.
- **Business logic** lives in Application services. MCP tool classes stay thin: validate inputs, call a service, return a DTO.
- **Services** use typed DTOs, take a `CancellationToken` everywhere, and get time from `TimeProvider`.
- **Package versions** are managed centrally in `Directory.Packages.props`. Don't put versions in csproj files. Keep every EF Core package on the same patch version.
- **Persistence**: stores are singletons that use `IDbContextFactory`, with one short-lived context per call. Tables and columns are snake_case. Stats only ever live in snapshot tables, never on `Video` or `Channel`.
- **Tests** are xUnit v3. Unit tests never hit the network or the database. YouTube is mocked at the HTTP level with saved fixtures.

## Environment

- macOS, Apple silicon, with .NET 10 SDK and PostgreSQL 17 from Homebrew. Docker isn't installed and isn't used.
- Registered with Claude Code at user scope (`claude mcp get personalvidiq`). It runs the published copy: `/opt/homebrew/bin/dotnet ~/.local/share/personalvidiq/personalvidiq.dll mcp`. After code changes, re-publish (`dotnet publish src/PersonalVidIQ.Host -c Release -o ~/.local/share/personalvidiq`) and start a new Claude session.
- Git remote: none yet. GitHub CLI is logged in as `amahdavid`. Ask before creating a repo or pushing.
