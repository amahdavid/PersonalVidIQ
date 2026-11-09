#!/bin/zsh
# Adds the PersonalVidIQ MCP server to Claude Desktop's config.
# Claude Desktop must be fully quit first, or it overwrites the file when it next quits.
if ps -axo comm= | grep -qx "/Applications/Claude.app/Contents/MacOS/Claude"; then
  echo "Claude Desktop is still running. Quit it with Cmd+Q, then run this again."
  exit 1
fi
cfg="$HOME/Library/Application Support/Claude/claude_desktop_config.json"
/usr/bin/python3 - "$cfg" <<'EOF'
import json, os, sys
p = sys.argv[1]
d = json.load(open(p))
d.setdefault("mcpServers", {})["personalvidiq"] = {
    "command": "/opt/homebrew/bin/dotnet",
    "args": [os.path.expanduser("~/.local/share/personalvidiq/personalvidiq.dll"), "mcp"],
}
json.dump(d, open(p, "w"), indent=2)
print("Added personalvidiq. Now reopen Claude Desktop.")
EOF
