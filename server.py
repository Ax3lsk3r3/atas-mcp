"""MCP server that exposes live ATAS Platform data and trading as AI tools.

The tools talk to the 'MCP Bridge' ChartStrategy running inside ATAS
(http://127.0.0.1:8787 by default). Connect this server to Claude Desktop,
Cursor, Claude Code, Antigravity, or any MCP-compatible client via stdio.

Run it with:
    pip install -r requirements.txt
    python server.py
"""

from __future__ import annotations

import os
import sys
from typing import Any

# Ensure atas_client is importable regardless of working directory
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from mcp.server.mcpserver import MCPServer

from atas_client import AtasClient, BridgeError

server = MCPServer(name="ATAS", version="1.0.0")
_client = AtasClient()


def _guard(client_fn):
    """Wrap a client call so MCP receives a structured error instead of a process crash."""
    try:
        return client_fn()
    except BridgeError as e:
        return {"ok": False, "error": str(e)}
    except Exception as e:
        return {"ok": False, "error": f"unexpected error: {e}"}


# ---------------------------------------------------------------------------
#  Market data tools
# ---------------------------------------------------------------------------

@server.tool(
    description="Connection status of the ATAS bridge: symbol, portfolio, "
                "tick size, whether the strategy is activated, bridge port, "
                "and current state. Call this first to verify the bridge is reachable."
)
def atas_status() -> dict[str, Any]:
    """Connection status of the ATAS bridge."""
    return _guard(lambda: _client.status())


@server.tool(
    description="Live quote snapshot: last traded price, best bid/ask with sizes, "
                "cumulative DOM bid/ask volume (order flow imbalance), spread, and tick size."
)
def atas_quote() -> dict[str, Any]:
    """Live quote snapshot."""
    return _guard(lambda: _client.quote())


@server.tool(
    description="Current market depth (DOM / order book): asks (lowest price first) "
                "and bids (highest price first), each level with price and volume. "
                "The 'levels' argument limits how many levels are returned per side."
)
def atas_dom(levels: int = 15) -> dict[str, Any]:
    """Current market depth (DOM / order book)."""
    data = _guard(lambda: _client.dom())
    if isinstance(data, dict) and "bids" in data and "asks" in data:
        return {
            "bids": data["bids"][:levels],
            "asks": data["asks"][:levels],
            "time": data.get("time"),
        }
    return data


@server.tool(
    description="Recent candle history for the chart the strategy is attached to "
                "(up to 2000 bars): open, high, low, close, volume, delta, and time, "
                "plus the current forming candle. Essential for context and technical analysis."
)
def atas_candles() -> dict[str, Any]:
    """Recent candle history."""
    return _guard(lambda: _client.candles())


# ---------------------------------------------------------------------------
#  Account and execution tools
# ---------------------------------------------------------------------------

@server.tool(
    description="Current open position details (volume, average price, direction, "
                "realized and unrealized PnL) derived from strategy fills."
)
def atas_position() -> dict[str, Any]:
    """Current open position details."""
    return _guard(lambda: _client.position())


@server.tool(
    description="All working (active) orders with ID, direction, type, price, "
                "trigger price, filled quantity, unfilled quantity, and state."
)
def atas_orders() -> dict[str, Any]:
    """All working orders."""
    return _guard(lambda: _client.orders())


@server.tool(
    description="Recent executed trades (fills) of the current session."
)
def atas_trades() -> dict[str, Any]:
    """Recent executed trades (fills)."""
    return _guard(lambda: _client.trades())


@server.tool(
    description="Everything at once: status, quote, DOM, candles, orders, position, "
                "and trades in a single call. Useful for full context on each turn."
)
def atas_snapshot() -> dict[str, Any]:
    """Everything at once (status, quote, DOM, candles, orders, position, trades)."""
    return _guard(lambda: _client.snapshot())


# ---------------------------------------------------------------------------
#  Trading tools
# ---------------------------------------------------------------------------

@server.tool(
    description="Place an order through ATAS. Parameters: direction ('buy' or 'sell'); "
                "order_type ('limit', 'stop', 'market', or 'stoplimit'); qty (number of contracts/lots); "
                "price (required for limit and stoplimit); trigger_price (required for stop and stoplimit); "
                "comment (optional order label). Prices are automatically snapped to instrument tick size."
)
def atas_place_order(
    direction: str,
    order_type: str,
    qty: float,
    price: float | None = None,
    trigger_price: float | None = None,
    comment: str | None = None,
) -> dict[str, Any]:
    """Place an order through ATAS."""
    return _guard(lambda: _client.place_order(
        direction=direction,
        order_type=order_type,
        qty=qty,
        price=price,
        trigger_price=trigger_price,
        comment=comment,
    ))


@server.tool(
    description="Cancel a working order by its ID (see atas_orders for working order IDs)."
)
def atas_cancel_order(order_id: str) -> dict[str, Any]:
    """Cancel a working order by ID."""
    return _guard(lambda: _client.cancel_order(order_id))


@server.tool(
    description="Flatten the current position using a market order. If volume and direction "
                "are omitted, the strategy automatically calculates the opposite direction "
                "and entire volume to close the position completely."
)
def atas_close_position(
    volume: float | None = None,
    direction: str | None = None,
) -> dict[str, Any]:
    """Flatten current position with a market order."""
    return _guard(lambda: _client.close_position(volume=volume, direction=direction))


# ---------------------------------------------------------------------------
#  Diagnostics tool
# ---------------------------------------------------------------------------

@server.tool(
    description="Recent diagnostic logs from the ATAS bridge running inside ATAS. "
                "Useful when troubleshooting strategy activation, tick processing, or order issues."
)
def atas_bridge_log() -> dict[str, Any]:
    """Recent diagnostic logs from the ATAS bridge."""
    return _guard(lambda: _client.log())


def main() -> None:
    """Entry point for the ATAS MCP server."""
    server.run()


if __name__ == "__main__":
    main()
