# ATAS MCP Bridge

[![CI](https://github.com/Ax3lsk3r3/atas-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/Ax3lsk3r3/atas-mcp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Python: 3.10+](https://img.shields.io/badge/python-3.10%2B-blue.svg)](https://www.python.org/downloads/)
[![MCP: 2.0](https://img.shields.io/badge/MCP-2.0-orange.svg)](https://modelcontextprotocol.io/)
[![Platform: ATAS 7.x & 8.x](https://img.shields.io/badge/ATAS-7.x%20%7C%208.x-green.svg)](https://atas.net/)
[![Release: v1.0.0](https://img.shields.io/badge/release-v1.0.0-brightgreen.svg)](https://github.com/Ax3lsk3r3/atas-mcp/releases)

Model Context Protocol (MCP) server connecting **ATAS Platform** (7.x) and **ATAS X** (8.x) to AI agents and development assistants.

[Leer en Espanol](README.es.md)

---

## Overview

ATAS MCP Bridge bridges the gap between institutional-grade order flow trading in ATAS and modern AI models. It exposes real-time market data, full order book depth (DOM), delta volume candles, account positions, open orders, trade executions, and automated order routing directly to AI assistants.

Compatible with all major AI coding agents and MCP clients:
- **OpenCode**
- **Qwen Code**
- **Kiro (CLI & IDE)**
- **Claude Code**
- **OpenAI Codex / Developers Platform**
- **OpenClaw**
- **Kimi Kode**
- **Z Code**
- **Google Antigravity (AGY CLI & IDE)**
- **Cursor**
- **Claude Desktop**
- **Windsurf / VS Code (Cline & Roo Code)**

---

## Architecture

```
+------------------------------------+          HTTP / SSE (127.0.0.1:8787)         +----------------------+
|           ATAS Platform            | <------------------------------------------> |   MCP Server (Py)    |
|            "MCP Bridge"            |             GET  /api/*                      |      server.py       |
|    (ChartStrategy in C# / .NET)    |             POST /api/order                  |      (mcp 2.0)       |
+------------------------------------+             GET  /api/stream                 +----------------------+
                                                                                               ^
                                                                                               | MCP (stdio)
                                                                                               v
                                                                                    +----------------------+
                                                                                    |      AI Clients      |
                                                                                    |  - OpenCode          |
                                                                                    |  - Qwen Code         |
                                                                                    |  - Kiro              |
                                                                                    |  - Claude Code       |
                                                                                    |  - OpenAI Codex      |
                                                                                    |  - OpenClaw          |
                                                                                    |  - Kimi Kode         |
                                                                                    |  - Z Code            |
                                                                                    |  - Antigravity       |
                                                                                    |  - Cursor            |
                                                                                    |  - Claude Desktop    |
                                                                                    +----------------------+
```

### Communication Flow:
1. **C# Addon (`ATAS.McpBridge`):** Runs inside the ATAS process as a native `ChartStrategy`. It hosts an embedded HTTP and Server-Sent Events (SSE) listener on `127.0.0.1:8787`.
2. **Thread Safety & Lock-Free Design:** All ATAS API calls occur strictly on the main ATAS thread within `OnCalculate()`. Incoming HTTP execution requests are queued in a thread-safe `ConcurrentQueue` and processed on the next tick. Read requests are answered immediately from volatile in-memory JSON snapshots without interrupting market data processing.
3. **Python MCP Server (`server.py`):** Speaks Model Context Protocol over standard I/O (`stdio`), exposing 12 production-ready tools with robust structured error handling.
4. **Python HTTP Client (`atas_client.py`):** Zero-dependency client using Python standard library with automated port discovery and environment variable support.

---

## MCP Tools (12 Tools)

| Tool | Description | Parameters |
|---|---|---|
| `atas_status` | Returns bridge connectivity status, symbol, portfolio account, tick size, activation state, and active port. | None |
| `atas_quote` | Live quote snapshot: last price, best bid and ask with volumes, DOM cumulative volume (order flow imbalance), spread, and tick size. | None |
| `atas_dom` | Full order book depth (DOM): sorted bids (highest first) and asks (lowest first) with exact price and size. | `levels` (int, default: 15) |
| `atas_candles` | Historical bars (up to 2000 candles) for the chart: open, high, low, close, volume, delta, timestamp, and the current forming candle. | None |
| `atas_position` | Current open position details: net volume, average entry price, direction (Buy/Sell), open PnL, and closed PnL. | None |
| `atas_orders` | Working orders: ID, direction, order type, price, trigger price, executed volume, remaining volume, and order state. | None |
| `atas_trades` | Execution log (fills) completed during the active session. | None |
| `atas_snapshot` | Consolidates status, quote, DOM, candles, orders, position, and trades in a single call. | None |
| `atas_place_order` | Places an order through ATAS. Prices are automatically rounded and snapped to instrument tick size. | `direction` ("buy"/"sell"), `order_type` ("limit"/"stop"/"market"/"stoplimit"), `qty` (float), `price` (optional float), `trigger_price` (optional float), `comment` (optional string) |
| `atas_cancel_order` | Cancels an open working order by its unique ID. | `order_id` (string) |
| `atas_close_position` | Closes (flattens) the current position using an opposing market order for the full or partial volume. | `volume` (optional float), `direction` (optional string) |
| `atas_bridge_log` | Retrieves recent diagnostic logs from the C# bridge inside ATAS for debugging. | None |

---

## HTTP REST and SSE Endpoints

The C# addon serves the following endpoints locally at `http://127.0.0.1:8787`:

| Endpoint | Method | Description |
|---|---|---|
| `/api/health` | GET | Health check (`{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}`). |
| `/api/status` | GET | Metadata: platform version, instrument, security code, portfolio, and tick size. |
| `/api/quote` | GET | Live ticker, best bid/ask, and cumulative order book imbalance. |
| `/api/dom` | GET | Full depth of market book snapshot. |
| `/api/candles` | GET | Array of completed candles plus current active bar. |
| `/api/orders` | GET | List of working orders. |
| `/api/position` | GET | Calculated net position and session PnL. |
| `/api/trades` | GET | Session trade executions (fills). |
| `/api/snapshot` | GET | All-in-one consolidated payload. |
| `/api/log` | GET | Diagnostic log buffer. |
| `/api/order` | POST | Submits a new order (`{"direction":"buy","type":"limit","qty":1,"price":22000.25}`). |
| `/api/order/cancel` | POST | Cancels an existing order (`{"id":"<order_id>"}`). |
| `/api/position/close` | POST | Flattens the open position (`{}`). |
| `/api/stream` | GET (SSE) | Real-time Server-Sent Events: `quote` (200ms throttle), `dom` (500ms throttle), `position` (1000ms throttle), `orderSent`, `orderFailed`, `orderCancelSent`, `flattenSent`. |

---

## Prerequisites

- **Operating System:** Windows 10 / 11 (x64)
- **Platform:** ATAS Platform (7.x) or ATAS X (8.x) installed
- **.NET SDK:** .NET 8 SDK or higher (`winget install Microsoft.DotNet.SDK.8` or `dotnet --version`)
- **Python:** Python 3.10 or higher with pip

---

## Installation and Quickstart

### 1. Clone the repository and install dependencies

```bash
git clone https://github.com/Ax3lsk3r3/atas-mcp.git
cd atas-mcp
pip install -r requirements.txt
```

Optionally install as an editable package so the `atas-mcp` CLI command is globally available:

```bash
pip install -e .
```

### 2. Compile and deploy the C# Addon

Run the build script:

```bash
build.bat
```

The script compiles the project in Release mode and copies `ATAS.McpBridge.dll` to both ATAS strategy folders:
- `%APPDATA%\ATAS\Strategies\` (ATAS Platform 7.x)
- `%APPDATA%\ATAS X\Strategies\` (ATAS X 8.x)

### 3. Attach the Strategy in ATAS

1. Launch ATAS (or ATAS X).
2. Open a chart for the instrument you want to trade (e.g. NQ, ES, BTCUSDT, EURUSD).
3. Right-click the chart -> select **Indicators** (or Add Indicator).
4. Search for **MCP Bridge** and add it to the chart.
5. In the strategy properties panel:
   - Check the **`IsActivated`** checkbox.
   - Select your portfolio / account (simulation or demo accounts recommended).
6. Verify in your web browser:
   - Navigate to `http://127.0.0.1:8787/api/health`
   - Expected output: `{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}`

### 4. Run the Automated Smoke Test

Verify the Python client against a local mock bridge without launching ATAS:

```bash
python test_client.py
```

All 13 automated tests should pass.

### 5. Start the MCP Server

The server supports two primary modes:

**Mode A: Stdio (for local desktop IDEs and CLIs):**
```bash
start_mcp.bat
# or directly:
python server.py
# or if installed via pip:
atas-mcp
```

**Mode B: Server-Sent Events (SSE) (for web applications, remote environments, cloud agents):**
```bash
start_mcp_sse.bat
# or directly:
python server.py --transport sse --host 127.0.0.1 --port 8000
# or with Docker:
docker compose up -d
```
The SSE endpoint will be available at `http://127.0.0.1:8000/sse` for web chat UIs (LibreChat, Open WebUI, AnythingLLM) and remote agent runners (OpenClaw, LangChain, CrewAI, AutoGen).

---

## Client Integration Examples

Detailed setup guides for all supported clients are provided in [mcp-config-examples.md](mcp-config-examples.md).

### OpenCode
Add to `~/.config/opencode/config.json` or project `opencode.json`:
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

### Qwen Code
Run via CLI:
```bash
qwen mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### Kiro (CLI & IDE)
Run via CLI:
```bash
kiro mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### Claude Code (CLI)
Run via CLI:
```bash
claude mcp add atas -- python "<PATH_TO_ATAS_MCP>/server.py"
```

### Google Antigravity (AGY)
Add to `%USERPROFILE%\.gemini\config\mcp_config.json`:
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

### Cursor
Add to `%USERPROFILE%\.cursor\mcp.json`:
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

### Claude Desktop
Add to `%APPDATA%\Claude\claude_desktop_config.json`:
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

## Technical Details

1. **ATAS 7.x and 8.x Compatibility:**
   - Targets .NET 8, which runs natively on ATAS 7.x and forwards cleanly into ATAS X .NET 10 cross-platform runtime.
   - Uses `IndicatorCandle` for full access to volume and delta statistics.
   - Price rounding respects `ShrinkPrice(price)` to prevent exchange reject errors.
   - Full order book depth snapshot extracted via `MarketDepthInfo.GetMarketDepthSnapshot()`.

2. **Position Tracking:**
   - In `ChartStrategy`, the net open volume and direction are computed from strategy-specific execution fills (`MyTrades`).
   - This ensures safe isolation: manual orders outside this strategy do not corrupt the algorithmic tracking of the strategy.

3. **Dynamic Port Discovery:**
   - If port 8787 is occupied, the C# bridge scans ports 8787 through 8807 automatically.
   - The selected port is stored in `%APPDATA%\ATAS\McpBridge.port` and `%APPDATA%\ATAS X\McpBridge.port`.
   - `atas_client.py` auto-discovers this file or falls back to the `ATAS_MCP_PORT` environment variable.

---

## Troubleshooting

| Issue | Likely Cause | Solution |
|---|---|---|
| `dotnet is not recognized` | .NET 8 SDK is missing or PATH is not refreshed. | Run `install_sdk.bat` or `winget install Microsoft.DotNet.SDK.8` and open a fresh terminal. |
| `http://127.0.0.1:8787/api/health` does not respond | Strategy is not added to a chart or `IsActivated` is unchecked. | Open ATAS, add `MCP Bridge` via chart Indicators, and check `IsActivated`. Check `%APPDATA%\ATAS\McpBridge.log`. |
| Network Access Denied on port bind | Windows HTTP.sys reservation missing. | Run in an Administrator Command Prompt: `netsh http add urlacl url=http://localhost:8787/ user=Everyone` (or language equivalent). |
| AI Client cannot find `atas_*` tools | Incorrect path in client MCP configuration file. | Verify that the path in your client JSON points to the absolute path of `server.py` on your machine. |
| Orders are rejected | Portfolio is not connected or demo connection inactive. | Verify in ATAS that your connector/broker status is green and select a valid portfolio in the strategy settings. |

---

## Risk Disclaimer

This software connects directly to financial trading platforms and can place binding orders on financial markets. Trading futures, equities, forex, and cryptocurrencies involves substantial risk of loss and is not suitable for every investor.

- Always test strategies, prompts, and tool calls thoroughly on simulated accounts (DEMO or Market Replay) before deploying capital.
- This software is distributed strictly for educational and technical automation purposes, without warranty of any kind.

---

## License

MIT License. See [LICENSE](LICENSE) for details.
