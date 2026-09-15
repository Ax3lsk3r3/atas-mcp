# ATAS MCP Bridge - Architecture and Technical Specification

Technical reference and architectural design document.
Status: Addon compiled and deployed, MCP server verified and tested.

---

## 1. Project Purpose

ATAS MCP Bridge provides a bidirectional bridge between the ATAS trading platform (Advanced Time And Sales Platform) and autonomous agents using the Model Context Protocol (MCP).

It enables advanced language models (LLMs) and coding agents to interact in real time with:
- Live market quotes and tick data with cumulative bid/ask volume imbalance.
- Full depth of market (DOM / Order Flow / Level 2 book).
- Up to 2000 historical OHLCV bars with bar-by-bar delta volume and active forming candle.
- Account state, working orders, fill executions, and derived net position.
- Order execution (Limit, Stop, Market, StopLimit), order cancellation, and position closure (flatten).

---

## 2. System Architecture Diagram

```
+---------------------------------------------------------------------------------------------+
|                                    ATAS PLATFORM PROCESS                                    |
|                                                                                             |
|  +---------------------------------------------------------------------------------------+  |
|  |                     McpBridgeStrategy (C# / .NET 8 / ATAS 7.x & 8.x)                  |  |
|  |                                                                                       |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  |  |                         Main Calculation Thread (ATAS Engine)                   |  |  |
|  |  |                                                                                 |  |  |
|  |  |  OnCalculate() / OnOrderChanged()                                               |  |  |
|  |  |   1. Ingests market ticks                                                       |  |  |
|  |  |   2. Updates volatile JSON snapshots (_quoteJson, _domJson, _candlesJson)       |  |  |
|  |  |   3. Drains action queue (DrainActions) and executes trading orders             |  |  |
|  |  |   4. Dispatches SSE stream events (PushEvent)                                   |  |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  |                                                                                       |  |
|  |  +-------------------------------------+     +-------------------------------------+  |  |
|  |  |      ConcurrentQueue<CommandRequest>|     |     Volatile JSON Snapshots         |  |  |
|  |  |  Thread-safe order queue            |     |  Ultra-fast lock-free read path     |  |  |
|  |  +-------------------------------------+     +-------------------------------------+  |  |
|  |                     ^                                           |                     |  |
|  |                     | Enqueue                                   | Atomic Read         |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  |  |                      Embedded HTTP / SSE Server (HttpListener)                  |  |  |
|  |  |                      Port: 127.0.0.1:8787 (or port range 8787..8807)            |  |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  +---------------------------------------------------------------------------------------+  |
+---------------------------------------------------------------------------------------------+
                                       ^
                                       | HTTP REST / SSE (localhost:8787)
                                       v
+---------------------------------------------------------------------------------------------+
|                                      MCP SERVER (PYTHON)                                    |
|                                                                                             |
|  +-----------------------------------+         +-----------------------------------------+  |
|  |           atas_client.py          | <-----> |                server.py                |  |
|  |  - Pure stdlib HTTP client        |         |  - MCP Server (mcp 2.0 MCPServer)       |  |
|  |  - Dynamic port auto-discovery    |         |  - 12 registered tools                  |  |
|  |  - Error wrapping & guarding      |         |  - Structured exception isolation       |  |
|  +-----------------------------------+         +-----------------------------------------+  |
+---------------------------------------------------------------------------------------------+
                                       ^
                                       | MCP Protocol (stdio / JSON-RPC)
                                       v
+---------------------------------------------------------------------------------------------+
|                                          AI CLIENTS                                         |
|                                                                                             |
|  OpenCode  |  Qwen Code  |  Kiro  |  Claude Code  |  OpenAI Codex  |  OpenClaw  |  Antigravity
+---------------------------------------------------------------------------------------------+
```

---

## 3. Components and Responsibilities

### A. C# Addon (`ATAS.McpBridge`)
- **Source location:** `ATAS.McpBridge/McpBridgeStrategy.cs`
- **Class:** `McpBridgeStrategy` (subclass of `ATAS.Strategies.Chart.ChartStrategy`).
- **Target Runtime:** .NET 8 (`net8.0-windows`), running natively on ATAS 7.x and forward-compatible with ATAS X (8.x).
- **External Dependencies:** Zero third-party dependencies. Built exclusively using core .NET libraries (`System.Net.HttpListener`, `System.Text.Json`, `System.Collections.Concurrent`).
- **Referenced ATAS Assemblies:**
  - `ATAS.DataFeedsCore.dll`
  - `ATAS.Indicators.dll`
  - `ATAS.Strategies.dll`
  - `ATAS.Types.dll`
  - `Utils.Common.dll` (resolves shared logging interfaces).

### B. Python MCP Server (`server.py`)
- Built using the official Model Context Protocol Python SDK (`mcp>=1.2.0`, `MCPServer`).
- Exposes 12 standardized `atas_*` tools over standard input/output (`stdio`).
- Implements `_guard()` wrapper that captures HTTP connection or runtime errors and returns structured diagnostic responses (`{"ok": false, "error": "..."}`) rather than crashing the process.

### C. Python HTTP Client (`atas_client.py`)
- Lightweight communication layer built solely with Python's standard library (`urllib.request`, `json`).
- Implements `_discover_port()`:
  1. Checks `ATAS_MCP_PORT` environment variable.
  2. Reads port file in `%APPDATA%\ATAS\McpBridge.port`.
  3. Reads port file in `%APPDATA%\ATAS X\McpBridge.port`.
  4. Defaults to `8787` if no port file is present.
- Connects to `127.0.0.1` by default to bypass IPv6 DNS resolution latency on Windows platforms.

---

## 4. Addon HTTP Endpoints

| Endpoint | Method | Parameters | Response |
|---|---|---|---|
| `/api/health` | GET | None | `{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}` |
| `/api/status` | GET | None | Connection metadata, instrument code, portfolio, tick size, activation state, active port |
| `/api/quote` | GET | None | Last price, best bid/ask, spread, cumulative DOM volume, tick size |
| `/api/dom` | GET | None | Sorted bid and ask depth levels with price and volume |
| `/api/candles` | GET | None | Up to 2000 closed candles plus active forming bar |
| `/api/orders` | GET | None | Working orders with filled/unfilled amounts and states |
| `/api/position` | GET | None | Net volume, direction, open PnL, closed PnL |
| `/api/trades` | GET | None | Executed fills completed in active session |
| `/api/snapshot` | GET | None | Consolidated payload combining all market and account data |
| `/api/log` | GET | None | Recent diagnostic log buffer from the bridge |
| `/api/order` | POST | `direction`, `type`, `qty`, `price?`, `triggerPrice?`, `comment?` | Order transmission acknowledgment and metadata |
| `/api/order/cancel` | POST | `id` | Cancellation request acknowledgment |
| `/api/position/close` | POST | `volume?`, `direction?` | Market close (flatten) order acknowledgment |
| `/api/stream` | GET (SSE) | None | Real-time Server-Sent Events continuous stream |

---

## 5. Specification of the 12 MCP Tools

1. `atas_status`: Overall bridge status and connectivity with ATAS.
2. `atas_quote`: Live quote ticker and order flow volume imbalance.
3. `atas_dom(levels: int = 15)`: Market depth filtered by number of levels per side.
4. `atas_candles`: Historical OHLCV candle series with volume delta.
5. `atas_position`: Open position summary and session PnL metrics.
6. `atas_orders`: Working orders inspection.
7. `atas_trades`: Executed trade fills in the current session.
8. `atas_snapshot`: Full market snapshot in a single tool call.
9. `atas_place_order(direction, order_type, qty, price?, trigger_price?, comment?)`: Order placement for Buy or Sell.
10. `atas_cancel_order(order_id)`: Order cancellation by order ID.
11. `atas_close_position(volume?, direction?)`: Position flattening via market order.
12. `atas_bridge_log`: Diagnostic log tail inspection for troubleshooting.

---

## 6. Technical Specifics: ATAS 7.x vs 8.x API

| Concept | ATAS 8.x (Online Docs) | ATAS 7.x (Local Engine) | Bridge Implementation |
|---|---|---|---|
| `GetCandle()` Return Type | `Candle` | `IndicatorCandle` | Uses `IndicatorCandle` with properties `Volume`, `Delta`, `Time`, `Open`, `High`, `Low`, `Close`. |
| `CurrentPosition` Property | Complex `Position` object | `decimal` (unrealized PnL) | Read as `decimal`. Net position (volume and direction) is computed by iterating `MyTrades`. |
| Price Snapping | Static extension method | Inherited protected member `ShrinkPrice(price)` | Calls `ShrinkPrice(price)` directly on `ChartStrategy`. |
| Security Data | `InstrumentInfo` | Properties on `Security` | Read directly from `Security` (`TickSize`, `Instrument`, etc.). |
| Best Bid / Best Ask | `MarketDataArg` | `MarketDataArg` in namespace `ATAS.Indicators` | Retrieved via `BestBid` and `BestAsk`. |
| Depth of Market (DOM) | Discrete events | `MarketDepthInfo.GetMarketDepthSnapshot()` | Snapshots `MarketDepthInfo` levels, partitioned into `IsBid` and `IsAsk`. |
| Order State Extension | Enum property | Extension method `Order.Status()` | Imported from namespace `ATAS.DataFeedsCore`. |
| Calculation Loop | `base.OnCalculate()` | Abstract method | Do not invoke `base.OnCalculate()` in `ChartStrategy`. |

---

## 7. Concurrency and Safety Design

1. **ATAS Thread Isolation:**
   - The ATAS core API is not thread-safe for external background threads.
   - HTTP modification requests (`/api/order`, `/api/order/cancel`, `/api/position/close`) are packaged into `CommandRequest` instances with an associated `TaskCompletionSource<string>` and enqueued in a `ConcurrentQueue`.
   - When ATAS calls `OnCalculate()` on each market tick or order update, `DrainActions()` drains the queue and executes the action on the native ATAS thread, resolving the asynchronous HTTP response task.

2. **Lock-Free Read Snapshots:**
   - During `OnCalculate()`, volatile string references (`_statusJson`, `_quoteJson`, `_domJson`, etc.) are swapped atomically.
   - Incoming GET requests read these references directly without locking or waiting on ticks.

3. **Port Recovery & Multi-Instance Handling:**
   - If port 8787 is busy, the bridge automatically iterates through ports 8787 to 8807.
   - The active port is saved to `%APPDATA%\ATAS\McpBridge.port` and `%APPDATA%\ATAS X\McpBridge.port`.

---

## 8. Verification & Test Suite

- **.NET Build:** 0 errors, 0 warnings (Release mode .NET 8).
- **Deployment:** Automatically mirrored to `%APPDATA%\ATAS\Strategies\` and `%APPDATA%\ATAS X\Strategies\`.
- **Smoke Test:** `test_client.py` with 13 automated tests completed in < 1 second.
- **MCP Compatibility:** Verified against MCP 2.0 specification over standard I/O.
