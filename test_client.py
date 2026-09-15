"""Smoke test: runs atas_client.py against a fake HTTP bridge (stub server)
so the client and server logic can be validated without ATAS running.

Usage:  python test_client.py
"""

from __future__ import annotations

import json
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer

from atas_client import AtasClient, BridgeError

PORT = 18987

CANNED = {
    "/api/health": {"ok": True, "id": "atas-mcp-bridge", "version": "1.0.0"},
    "/api/status": {"id": "atas-mcp-bridge", "symbol": "NQZ6", "portfolio": "SIM", "tickSize": 0.25, "activated": True, "port": PORT},
    "/api/quote": {"last": 22000.25, "bestBid": 22000.00, "bestAsk": 22000.50, "bidSize": 12, "askSize": 8, "cumBids": 1400, "cumAsks": 1200, "spread": 0.5, "tickSize": 0.25},
    "/api/dom": {"bids": [{"price": 22000.00, "volume": 12, "side": "Bid"}], "asks": [{"price": 22000.50, "volume": 8, "side": "Ask"}], "time": "2026-01-01T00:00:00Z"},
    "/api/candles": {"candles": [{"open": 21999, "high": 22001, "low": 21998, "close": 22000.25, "volume": 500, "delta": 12, "time": "2026-01-01T00:00:00Z"}], "current": None},
    "/api/orders": [],
    "/api/position": {"inPosition": False, "volume": 0},
    "/api/trades": [],
    "/api/log": {"log": ["bridge initialized", "listening on port 18987"]},
}


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):  # noqa: N802
        if self.path == "/api/snapshot":
            body = json.dumps({k.replace("/api/", ""): v for k, v in CANNED.items()}).encode()
        else:
            body = json.dumps(CANNED.get(self.path, {"ok": False, "error": "missing stub"})).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):  # noqa: N802
        length = int(self.headers.get("Content-Length", 0))
        raw = self.rfile.read(length) if length else b"{}"
        body = json.dumps({"ok": True, "echo": json.loads(raw)}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *args):
        pass


def main() -> None:
    server = HTTPServer(("127.0.0.1", PORT), Handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()

    client = AtasClient(port=PORT, host="127.0.0.1")

    checks = [
        ("health", client.health(), "ok", True),
        ("status", client.status(), "symbol", "NQZ6"),
        ("quote", client.quote(), "bestAsk", 22000.50),
        ("dom", client.dom(), "asks_len", 1),
        ("candles", client.candles(), "candles_len", 1),
        ("orders", client.orders(), "is_list", True),
        ("position", client.position(), "is_dict", True),
        ("trades", client.trades(), "is_list", True),
        ("snapshot", client.snapshot(), "is_dict", True),
        ("log", client.log(), "is_dict", True),
        ("place_order", client.place_order("buy", "limit", 2, price=22000.25), "ok", True),
        ("cancel_order", client.cancel_order("abc"), "ok", True),
        ("close_position", client.close_position(), "ok", True),
    ]

    failures = 0
    for name, result, field, expected in checks:
        ok = False
        if field == "is_list":
            ok = isinstance(result, list)
        elif field == "is_dict":
            ok = isinstance(result, dict)
        elif field == "asks_len":
            ok = len(result.get("asks", [])) == expected
        elif field == "candles_len":
            ok = len(result.get("candles", [])) == expected
        else:
            ok = result.get(field) == expected
        status = "PASS" if ok else "FAIL"
        if not ok:
            failures += 1
        print(f"[{status}] {name}: {result}")

    # error path: unreachable port -> BridgeError
    dead = AtasClient(port=PORT + 1, host="127.0.0.1")
    try:
        dead._request("/api/health", timeout=0.5)
        print("[FAIL] expected BridgeError for unreachable bridge")
        failures += 1
    except BridgeError:
        print("[PASS] unreachable bridge raises BridgeError")

    server.shutdown()
    print("\n" + ("ALL TESTS PASSED" if failures == 0 else f"{failures} TEST(S) FAILED"))
    raise SystemExit(1 if failures else 0)


if __name__ == "__main__":
    main()
