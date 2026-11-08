# PersonalVidIQ

A private, local-only YouTube research tool that Claude (Desktop or Code) uses through a local
stdio MCP server. Public YouTube Data API data, PostgreSQL on this Mac, $0/month.

Scope and every settled decision: [DECISIONS.md](DECISIONS.md). The original brief is in Notion
("Personal vidIQ Alternative — Implementation Brief"); where they differ, DECISIONS.md wins.

## Layout

| Project | Role |
|---|---|
| `src/PersonalVidIQ.Domain` | Entities, enums (`MetricSource`). No dependencies. |
| `src/PersonalVidIQ.Application` | Service interfaces, DTOs, options, analytics. Business logic lives here. |
| `src/PersonalVidIQ.Infrastructure` | YouTube client, EF Core/PostgreSQL, implementations of the interfaces. |
| `src/PersonalVidIQ.Mcp` | Thin MCP tool classes that call application services. |
| `src/PersonalVidIQ.Host` | The `personalvidiq` executable: `mcp` (stdio server) and `status`. |
| `tests/PersonalVidIQ.UnitTests` | Fast tests, no network or database. |
| `tests/PersonalVidIQ.IntegrationTests` | Real local PostgreSQL, and the real server over stdio. |

## One-time setup

```bash
brew install dotnet postgresql@17
brew services start postgresql@17
createdb personalvidiq
createdb personalvidiq_test
```

Add your YouTube Data API key (Google Cloud console → APIs & Services → enable "YouTube Data API v3" → Credentials → API key).
It is stored in .NET user secrets, outside this folder:

```bash
dotnet user-secrets set "YouTube:ApiKey" "<key>" --project src/PersonalVidIQ.Host
```

Create the tables (the MCP server also does this on startup):

```bash
dotnet run --project src/PersonalVidIQ.Host -- migrate
```

Check everything:

```bash
dotnet run --project src/PersonalVidIQ.Host -- status
```

## Build and test

```bash
dotnet build
dotnet test
```

Integration tests use the `personalvidiq_test` database (override with `PERSONALVIDIQ_TEST_DB`).
The live YouTube test runs only when `PERSONALVIDIQ_YOUTUBE_API_KEY` is set; it costs about 3 quota units.

## Connect Claude

Publish a stable copy so Claude doesn't run from the build folder:

```bash
dotnet publish src/PersonalVidIQ.Host -c Release -o ~/.local/share/personalvidiq
```

**Claude Code:**

```bash
claude mcp add --scope user personalvidiq -- /opt/homebrew/bin/dotnet ~/.local/share/personalvidiq/personalvidiq.dll mcp
```

**Claude Desktop:** add to `~/Library/Application Support/Claude/claude_desktop_config.json` (use the full path; `~` is not expanded):

```json
{
  "mcpServers": {
    "personalvidiq": {
      "command": "/opt/homebrew/bin/dotnet",
      "args": ["/Users/<you>/.local/share/personalvidiq/personalvidiq.dll", "mcp"]
    }
  }
}
```

Then ask Claude to "run get_status on personalvidiq".

User secrets are read from `~/.microsoft/usersecrets/`, so the published copy picks up the same API key.
stdout is reserved for the MCP protocol; logs go to stderr (Claude Desktop shows them in its MCP logs).

## Data retention

YouTube's API policies allow storing public statistics for at most 30 days. PersonalVidIQ deletes snapshots
older than `DataCollection:SnapshotRetentionDays` (max 30), and untracked videos/channels not refreshed in 30 days.
This runs whenever the MCP server starts, or on demand:

```bash
dotnet run --project src/PersonalVidIQ.Host -- purge
```

## Configuration

Defaults live in `src/PersonalVidIQ.Host/appsettings.json`. Override with user secrets or environment
variables (`YouTube__DailySearchCallCap=20`). See `.env.example` for names. `SnapshotRetentionDays`
cannot exceed 30 (YouTube API Developer Policies III.E.4).
