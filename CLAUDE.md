# PersonalVidIQ

A private, local-only YouTube research tool for the user's football Shorts channel. Claude (Desktop/Code) is the only client, through a **local stdio MCP server**. Nothing runs online: no HTTP endpoint, tunnel, OAuth or ChatGPT.

## Sources of truth

1. **[DECISIONS.md](DECISIONS.md)**: every settled decision. Read it before changing scope or behaviour.
2. The Notion brief "Personal vidIQ Alternative — Implementation Brief" (`411bce3163cf81738d0bc965debedf67`). ChatGPT wrote it, so read every "ChatGPT" in it as "Claude". Where it disagrees with DECISIONS.md, DECISIONS.md wins. Don't edit Notion without asking.
3. [README.md](README.md): setup, and how to connect Claude.

When a new decision is made with the user, add it to DECISIONS.md in the same change.

## Status

- [x] Milestone 0: solution, stdio host, `get_status` tool, Postgres, tests
- [ ] Milestone 1: YouTube Data API client (search, channels, videos, quota tracking, caching)
- [ ] Milestone 2: EF Core entities, migrations, 30-day snapshot purge
- [ ] Milestone 3: analytics (velocity, outliers, comparisons)
- [ ] Milestone 4: the MVP MCP tools, including tracking tools
- [ ] Milestone 5: `collect` command (on demand, no scheduler)
- [ ] Milestone 6: research features (no autocomplete)

Update this list when a milestone lands.

## Commands

```bash
dotnet build                                          # warnings are errors
dotnet test                                           # Microsoft.Testing.Platform (set in global.json)
dotnet run --project src/PersonalVidIQ.Host -- status # setup/health report
```

Integration tests need local Postgres (`brew services start postgresql@17`) and the `personalvidiq_test` database.

## Rules that are easy to break

- **stdout belongs to the MCP protocol.** Never `Console.WriteLine` in the `mcp` path. Logs go to stderr only.
- **30-day limit:** YouTube API Developer Policies III.E.4 say non-authorized statistics (views, likes, subscribers) may not be stored for more than 30 days. Snapshots get purged, and `SnapshotRetentionDays` is capped at 30. Don't add long-term history.
- **Label every metric** with `MetricSource`: YouTube API, calculated by PersonalVidIQ, third-party estimate, or unavailable. Calculated numbers must say they're not YouTube metrics (policy III.E.4.h). Never invent search volume, CTR, impressions, retention or history that wasn't collected.
- **Quota:** `search.list` costs 100 units, while `videos.list`, `channels.list` and `playlistItems.list` cost 1. Channel videos come from the uploads playlist, never search. Batch video IDs 50 at a time. Search has a daily cap and a 24 h cache.
- **Shorts are 180 s or less** (configurable). Never mix Shorts and long-form in one outlier cohort.
- **No scraping** and no undocumented endpoints. That includes YouTube autocomplete.
- **$0/month.** No paid APIs or services, and no OpenAI or Anthropic API calls from the app. Ask before adding any dependency that costs money, and before installing software.
- **Keep secrets out of the repo.** The API key lives in .NET user secrets (`YouTube:ApiKey`). Never log keys or connection strings.

## Architecture

The layers are Domain → Application → Infrastructure, with Mcp and Host on top.

- **Business logic** lives in Application services. MCP tool classes stay thin: validate inputs, call a service, return a DTO.
- **Services** use typed DTOs, take a `CancellationToken` everywhere, and get time from `TimeProvider`.
- **Package versions** are managed centrally in `Directory.Packages.props`. Don't put versions in csproj files.
- **Tests** are xUnit v3. Unit tests never hit the network or the database. YouTube is mocked at the HTTP level with saved fixtures.

## Environment

- macOS, Apple silicon, with .NET 10 SDK and PostgreSQL 17 from Homebrew. Docker isn't installed and isn't used.
- Claude launches the server as `/opt/homebrew/bin/dotnet ~/.local/share/personalvidiq/personalvidiq.dll mcp` (the published copy). Re-publish after changes before testing in Claude.
- Git remote: none yet. GitHub CLI is logged in as `amahdavid`. Ask before creating a repo or pushing.
