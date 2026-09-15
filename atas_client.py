"""Minimal HTTP client for the ATAS MCP Bridge (ChartStrategy running inside ATAS).

Only uses the Python standard library, so there are no extra dependencies.
"""

from __future__ import annotations

import json
import os
import urllib.error
import urllib.request

DEFAULT_PORT = 8787
DEFAULT_HOST = "127.0.0.1"
PORT_FILES = [
    os.path.join(os.environ.get("APPDATA", ""), "ATAS", "McpBridge.port"),
    os.path.join(os.environ.get("APPDATA", ""), "ATAS X", "McpBridge.port"),
]


class BridgeError(Exception):
    """Raised when the ATAS bridge cannot be reached or returns an error."""


def _discover_port() -> int:
    """Read the port file written by the ATAS bridge, falling back to 8787."""
    env_port = os.environ.get("ATAS_MCP_PORT")
    if env_port:
        try:
            port = int(env_port.strip())
            if 1 <= port <= 65535:
                return port
        except ValueError:
            pass

    for port_file in PORT_FILES:
        try:
            with open(port_file, "r", encoding="utf-8") as f:
                port = int(f.read().strip())
                if 1 <= port <= 65535:
                    return port
        except Exception:
            pass
    return DEFAULT_PORT


class AtasClient:
    """Thin wrapper around the ATAS bridge HTTP endpoints."""

    def __init__(self, port: int | None = None, host: str | None = None):
        self.port = port if port is not None else _discover_port()
        self.host = host if host is not None else os.environ.get("ATAS_MCP_HOST", DEFAULT_HOST)
        self.base = f"http://{self.host}:{self.port}"

    # -- low level -----------------------------------------------------------
    def _request(self, path: str, payload: dict | None = None, timeout: float = 10.0) -> dict:
        url = self.base + path
        data = None
        headers = {}
        if payload is not None:
            data = json.dumps(payload).encode("utf-8")
            headers["Content-Type"] = "application/json"
        try:
            req = urllib.request.Request(url, data=data, headers=headers)
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                result = json.loads(resp.read().decode("utf-8"))
        except urllib.error.URLError as e:
            raise BridgeError(
                f"cannot reach the ATAS bridge at {url}. "
                f"Make sure ATAS is running with the 'MCP Bridge' strategy active on a chart. ({e})"
            ) from e
        if isinstance(result, dict) and result.get("ok") is False:
            raise BridgeError(result.get("error", "bridge returned an error"))
        return result

    # -- read endpoints ------------------------------------------------------
    def health(self) -> dict:
        return self._request("/api/health")

    def status(self) -> dict:
        return self._request("/api/status")

    def quote(self) -> dict:
        return self._request("/api/quote")

    def dom(self) -> dict:
        return self._request("/api/dom")

    def candles(self) -> dict:
        return self._request("/api/candles")

    def orders(self) -> dict:
        return self._request("/api/orders")

    def position(self) -> dict:
        return self._request("/api/position")

    def trades(self) -> dict:
        return self._request("/api/trades")

    def snapshot(self) -> dict:
        return self._request("/api/snapshot")

    def log(self) -> dict:
        return self._request("/api/log")

    # -- trading endpoints ---------------------------------------------------
    def place_order(
        self,
        direction: str,
        order_type: str,
        qty: float,
        price: float | None = None,
        trigger_price: float | None = None,
        comment: str | None = None,
    ) -> dict:
        """Place an order. direction: buy|sell, order_type: limit|stop|market|stoplimit."""
        payload: dict = {"direction": direction, "type": order_type, "qty": qty}
        if price is not None:
            payload["price"] = price
        if trigger_price is not None:
            payload["triggerPrice"] = trigger_price
        if comment is not None:
            payload["comment"] = comment
        return self._request("/api/order", payload)

    def cancel_order(self, order_id: str) -> dict:
        return self._request("/api/order/cancel", {"id": order_id})

    def close_position(self, volume: float | None = None, direction: str | None = None) -> dict:
        payload: dict = {}
        if volume is not None:
            payload["volume"] = volume
        if direction is not None:
            payload["direction"] = direction
        return self._request("/api/position/close", payload)
