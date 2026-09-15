# ATAS MCP Client Configuration Guide

The ATAS MCP server (`server.py`) communicates over standard input/output (stdio) following the Model Context Protocol (MCP) specification. Any MCP-compliant client, IDE, or AI agent runtime can connect to it.

You can launch the server using either:
- **Direct script execution:** `python <PATH_TO_ATAS_MCP>/server.py`
- **Installed CLI command:** run `pip install -e .` in the repository root, then use `atas-mcp`

Replace `<PATH_TO_ATAS_MCP>` in the examples below with the absolute path to your local clone (for example: `C:/Projects/atas-mcp` or `C:\\Projects\\atas-mcp`).

---

## 1. OpenCode

Configuration file:
- Linux/macOS: `~/.config/opencode/config.json`
- Windows: `%USERPROFILE%\.config\opencode\config.json` (or `opencode.json` in your project root)

```json
{
  "mcp": {
    "atas": {
      "type": "stdio",
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ]
    }
  }
}
```

If installed via pip:

```json
{
  "mcp": {
    "atas": {
      "type": "stdio",
      "command": "atas-mcp"
    }
  }
}
```

Reference: [OpenCode Documentation](https://opencode.ai/docs)

---

## 2. Qwen Code

CLI command:

```bash
qwen mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

Or configure directly in `%USERPROFILE%\.qwen\mcp.json`:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ]
    }
  }
}
```

Reference: [Qwen Code Overview](https://qwenlm.github.io/qwen-code-docs/en/users/overview/)

---

## 3. Kiro (CLI & IDE)

CLI command:

```bash
kiro mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

Or configure in `%USERPROFILE%\.kiro\mcp.json`:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ]
    }
  }
}
```

Reference: [Kiro Documentation](https://kiro.dev/docs/)

---

## 4. Claude Code (CLI)

Add via the Claude Code CLI:

```bash
claude mcp add atas -- python "<PATH_TO_ATAS_MCP>/server.py"
```

If installed with `pip install -e .`:

```bash
claude mcp add atas -- atas-mcp
```

Reference: [Claude Code](https://claude.com/product/claude-code)

---

## 5. OpenAI Codex / OpenAI Developers Platform

When building agentic workflows with OpenAI's Developers Platform or Agents SDK:

```python
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

server_params = StdioServerParameters(
    command="python",
    args=["<PATH_TO_ATAS_MCP>/server.py"],
)

# Connect and expose ATAS tools to your OpenAI Agent / Responses pipeline
async with stdio_client(server_params) as (read, write):
    async with ClientSession(read, write) as session:
        await session.initialize()
        tools = await session.list_tools()
        # Bind tools to OpenAI client
```

Reference: [OpenAI Developers Platform](https://developers.openai.com/)

---

## 6. OpenClaw

In your OpenClaw agent configuration (`openclaw.json` or workspace configuration):

```json
{
  "tools": {
    "mcpServers": {
      "atas": {
        "command": "python",
        "args": [
          "<PATH_TO_ATAS_MCP>/server.py"
        ],
        "cwd": "<PATH_TO_ATAS_MCP>"
      }
    }
  }
}
```

Reference: [OpenClaw Documentation](https://docs.openclaw.ai/)

---

## 7. Kimi Kode

CLI command:

```bash
kimi mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

Or configure in `%USERPROFILE%\.kimi\mcp.json`:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ]
    }
  }
}
```

Reference: [Kimi Kode Documentation](https://www.kimi.com/code/docs/en/)

---

## 8. Z Code

CLI command:

```bash
zcode mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

Or configure in `%USERPROFILE%\.zcode\config.json`:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ]
    }
  }
}
```

Reference: [Z Code Documentation](https://zcode.z.ai/en/docs/welcome)

---

## 9. Google Antigravity (AGY CLI & IDE)

Configuration file: `%USERPROFILE%\.gemini\config\mcp_config.json`

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ]
    }
  }
}
```

---

## 10. Cursor

Configuration file: `%USERPROFILE%\.cursor\mcp.json`

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ]
    }
  }
}
```

Via GUI:
1. Open Cursor Settings (Ctrl + ,).
2. Navigate to the MCP section.
3. Click "Add new MCP server".
4. Type: `stdio`.
5. Command: `python`.
6. Arguments: `<PATH_TO_ATAS_MCP>/server.py`.

---

## 11. Claude Desktop

Configuration file: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>\\server.py"
      ],
      "cwd": "<PATH_TO_ATAS_MCP>"
    }
  }
}
```

Restart Claude Desktop after saving the configuration.

---

## 12. Windsurf / VS Code (Cline / Roo Code)

Configuration file:
- Cline: `%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`
- Roo Code: `%APPDATA%\Code\User\globalStorage\rooveterinaryinc.roo-cline\settings\cline_mcp_settings.json`

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ],
      "disabled": false,
      "autoApprove": []
    }
  }
}
```

---

## Environment Variables

Custom settings can be passed to the MCP server via environment variables:

| Variable | Default | Description |
|---|---|---|
| `ATAS_MCP_PORT` | `8787` | Port where the ATAS C# addon HTTP bridge listens. If not set, the port file written by the addon is auto-discovered. |
| `ATAS_MCP_HOST` | `127.0.0.1` | Host where the HTTP bridge is running. Defaults to local loopback `127.0.0.1`. |

Example using environment variables in MCP JSON:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "<PATH_TO_ATAS_MCP>/server.py"
      ],
      "env": {
        "ATAS_MCP_PORT": "8787",
        "ATAS_MCP_HOST": "127.0.0.1"
      }
    }
  }
}
```
