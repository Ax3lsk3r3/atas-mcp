# Showcase: ATAS Model Context Protocol (MCP) Bridge

## Subject / Title
[Community Integration] ATAS Model Context Protocol (MCP) Bridge - Open-source AI assistant integration

## Target
Repository: AtasPlatform/Indicators (GitHub Issues)
Email: contact@atas.net

---

### Body

Hello ATAS team and community,

We would like to share an open-source integration built for ATAS and ATAS X that brings Model Context Protocol (MCP) support to the platform:

Repository: https://github.com/Ax3lsk3r3/atas-mcp

### Overview
This project bridges ATAS with modern MCP-compatible AI coding assistants, autonomous agents, and analytics environments (including Cursor, Claude Code, Antigravity, OpenCode, Kiro, Qwen Code, and custom agent workflows).

It enables AI assistants to inspect real-time market state, analyze order flow, and assist with trading operations through 12 dedicated tools:
- Real-time market data (quotes, bid/ask spread, volume)
- Full Level 2 Depth of Market (DOM) inspection
- Historical and live candlestick data
- Position tracking, account balance, and execution history
- Order lifecycle management (active, filled, cancelled)
- Order placement with configurable risk management safeguards (take-profit, stop-loss, drawdown limits)

### Technical Architecture
1. C# Addon (ATAS.McpBridge):
   - Built on .NET 8 / C# using the ATAS Indicator/Strategy API.
   - Compatible with both ATAS Platform 7.x (%APPDATA%\ATAS\Strategies) and ATAS X 8.x (%APPDATA%\ATAS X\Strategies).
   - Exposes an embedded, low-latency HTTP server on 127.0.0.1:8787.
2. Python MCP Server (atas-mcp):
   - Supports standard local stdio transport (native process, zero configuration, no containers required).
   - Also supports SSE and streamable-http transports for remote or web-based agents.
   - Comprehensive test suite and schema validation.

### Motivation and Collaboration
Our goal is to expand the ATAS ecosystem by connecting it with the open AI standards currently being adopted across the developer community.

We would welcome any feedback from the ATAS engineering team regarding:
- Best practices or recommendations for custom Strategy/Indicator API usage.
- Potential inclusion in community showcase resources or developer listings.

The project is fully open source under the MIT license. Thank you for maintaining such an extensible platform.
