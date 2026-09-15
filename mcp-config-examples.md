# ATAS MCP Client Configuration & Universal Connection Guide

The ATAS MCP server (`server.py`) supports three connection transports to accommodate any client environment:
- **stdio (Standard Input/Output):** Recommended for local desktop IDEs, terminal CLIs, and local agent processes.
- **sse (Server-Sent Events over HTTP):** Recommended for web applications, remote environments, cloud agents, Docker containers, and browser-based AI chats.
- **streamable-http:** Modern streaming HTTP protocol for stateful and stateless web-native agent architectures.

---

## Running the Server

### Option A: Local Stdio Mode (Default)
Used when the client process launches `server.py` directly:
```bash
python server.py
# or via globally installed CLI:
atas-mcp
```

### Option B: HTTP / SSE Mode (Port 8000)
Used when connecting web applications, remote agents, or multiple clients concurrently:
```bash
python server.py --transport sse --host 127.0.0.1 --port 8000
# or on Windows:
start_mcp_sse.bat
# or in Docker:
docker compose up -d
```
The SSE endpoint will be live at `http://127.0.0.1:8000/sse` with message routing at `http://127.0.0.1:8000/messages/`.

---

## 1. OpenCode

Website: https://opencode.ai
Documentation: https://opencode.ai/docs

### Local Desktop / CLI (stdio)
In `~/.config/opencode/config.json` (Linux/macOS) or `%USERPROFILE%\.config\opencode\config.json` (Windows), or `opencode.json` in your workspace root:

```json
{
  "mcp": {
    "atas": {
      "type": "stdio",
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>/server.py"]
    }
  }
}
```

### Remote Workspace / Web IDE (SSE)
```json
{
  "mcp": {
    "atas": {
      "type": "sse",
      "url": "http://127.0.0.1:8000/sse"
    }
  }
}
```

---

## 2. Qwen Code

Overview: https://qwenlm.github.io/qwen-code-docs/en/users/overview/
Repository: https://github.com/QwenLM/qwen-code

### CLI Command (stdio)
```bash
qwen mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### CLI Command (SSE)
```bash
qwen mcp add --type sse atas http://127.0.0.1:8000/sse
```

### Configuration File (`%USERPROFILE%\.qwen\mcp.json`)
```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>/server.py"]
    }
  }
}
```

---

## 3. Kiro (CLI & Desktop IDE)

Website: https://kiro.dev
CLI Reference: https://kiro.dev/cli/

### Local CLI & Desktop (stdio)
```bash
kiro mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### Web & Cloud Environments (SSE)
```bash
kiro mcp add --type sse atas http://127.0.0.1:8000/sse
```

### Configuration File (`%USERPROFILE%\.kiro\mcp.json`)
```json
{
  "mcpServers": {
    "atas-stdio": {
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>/server.py"]
    },
    "atas-sse": {
      "url": "http://127.0.0.1:8000/sse"
    }
  }
}
```

---

## 4. Claude Code (CLI)

Product: https://claude.com/product/claude-code

### Stdio Transport
```bash
claude mcp add atas -- python "<PATH_TO_ATAS_MCP>/server.py"
```
Or if installed via `pip install -e .`:
```bash
claude mcp add atas -- atas-mcp
```

### SSE Transport (Remote / Shared)
```bash
claude mcp add --transport sse atas http://127.0.0.1:8000/sse
```

---

## 5. OpenAI Codex / Agents SDK

Documentation: https://developers.openai.com/

### Stdio Client in Python:
```python
import asyncio
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

async def run():
    server_params = StdioServerParameters(
        command="python",
        args=["<PATH_TO_ATAS_MCP>/server.py"],
    )
    async with stdio_client(server_params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()
            print("Connected tools:", [t.name for t in tools.tools])

asyncio.run(run())
```

### SSE Client in Python (Web / Microservices):
```python
import asyncio
from mcp import ClientSession
from mcp.client.sse import sse_client

async def run():
    async with sse_client("http://127.0.0.1:8000/sse") as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            quote = await session.call_tool("atas_quote", {})
            print("Quote:", quote.content)

asyncio.run(run())
```

---

## 6. OpenClaw (Autonomous Trading Agents)

Website: https://openclaw.ai
Documentation: https://docs.openclaw.ai

In `openclaw.json` or agent runner config:

### Stdio Configuration
```json
{
  "tools": {
    "mcpServers": {
      "atas": {
        "command": "python",
        "args": ["<PATH_TO_ATAS_MCP>/server.py"],
        "cwd": "<PATH_TO_ATAS_MCP>"
      }
    }
  }
}
```

### Remote Container / Web Gateway (SSE)
```json
{
  "tools": {
    "mcpServers": {
      "atas": {
        "type": "sse",
        "url": "http://127.0.0.1:8000/sse"
      }
    }
  }
}
```

---

## 7. Kimi Kode

Website: https://www.kimi.com/code/en
Documentation: https://www.kimi.com/code/docs/en/

### CLI Command
```bash
kimi mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### Configuration (`%USERPROFILE%\.kimi\mcp.json`)
```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>/server.py"]
    }
  }
}
```

---

## 8. Z Code

Website: https://zcode.z.ai/en/docs/welcome

### CLI Command
```bash
zcode mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### Configuration (`%USERPROFILE%\.zcode\config.json`)
```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>/server.py"]
    }
  }
}
```

---

## 9. Google Antigravity (AGY CLI & IDE)

Configuration file: `%USERPROFILE%\.gemini\config\mcp_config.json`

### Stdio
```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>/server.py"]
    }
  }
}
```

### Remote / SSE
```json
{
  "mcpServers": {
    "atas": {
      "url": "http://127.0.0.1:8000/sse"
    }
  }
}
```

---

## 10. Cursor

Configuration file: `%USERPROFILE%\.cursor\mcp.json`

### Stdio Mode
```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>/server.py"]
    }
  }
}
```

### SSE Mode (Web / Remote Tunnels / Dev Containers)
```json
{
  "mcpServers": {
    "atas": {
      "type": "sse",
      "url": "http://127.0.0.1:8000/sse"
    }
  }
}
```

---

## 11. Claude Desktop

Configuration file: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": ["<PATH_TO_ATAS_MCP>\\server.py"],
      "cwd": "<PATH_TO_ATAS_MCP>"
    }
  }
}
```

---

## 12. Web UIs (LibreChat, Open WebUI, AnythingLLM)

Web-based chat interfaces cannot launch local subprocesses on the browser client; they connect directly to an SSE HTTP server:

1. Launch the server in SSE mode:
   ```bash
   python server.py --transport sse --host 0.0.0.0 --port 8000
   ```
2. In LibreChat (`librechat.yaml`) or Open WebUI MCP Settings:
   ```yaml
   mcpServers:
     atas:
       type: sse
       url: http://127.0.0.1:8000/sse
   ```

---

## 13. Generic Agent Frameworks (LangChain, CrewAI, AutoGen, LlamaIndex)

Any Python, Node.js, Go, or Rust agent framework supporting Model Context Protocol can consume ATAS MCP:

```python
# LangChain / LangGraph with MCP adapter
from langchain_mcp_adapters.tools import load_mcp_tools
from mcp.client.sse import sse_client

async with sse_client("http://127.0.0.1:8000/sse") as (read, write):
    async with ClientSession(read, write) as session:
        await session.initialize()
        tools = await load_mcp_tools(session)
        # Agent binds tools for trading execution and market analysis
```

---

## Environment Variables Reference

| Variable | Default | Description |
|---|---|---|
| `MCP_TRANSPORT` | `stdio` | Transport protocol: `stdio`, `sse`, or `streamable-http`. |
| `MCP_HOST` | `127.0.0.1` | Network interface to bind for SSE / streamable-http (use `0.0.0.0` inside Docker). |
| `MCP_PORT` | `8000` | Port for the MCP server when running in SSE / streamable-http mode. |
| `ATAS_MCP_HOST` | `127.0.0.1` | Host where the ATAS C# bridge is running. Defaults to `127.0.0.1`. |
| `ATAS_MCP_PORT` | `8787` | Port where the ATAS C# bridge is listening (auto-detected if omitted). |
