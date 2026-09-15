// =============================================================================
//  ATAS MCP Bridge - ChartStrategy that runs inside ATAS Platform (7.x)
//  Exposes live market data (quotes, DOM, candles, positions, orders, trades)
//  and order execution over a local HTTP + SSE server on http://localhost:8787/
//  so that an external MCP server (Python) can expose it to AI agents.
//
//  HOW IT WORKS
//  - All ATAS API calls happen on the ATAS thread (inside OnCalculate /
//    OnStarted / OnOrderChanged). Requests coming from the HTTP listener are
//    queued and drained from OnCalculate, which ATAS calls on every tick.
//  - Read endpoints serve from cached JSON snapshots (volatile strings), so
//    the HTTP threads never touch ATAS objects directly.
//  - The API used here was verified against the user's ATAS 7.0.9 DLLs
//    (metadata dump): IndicatorCandle, decimal CurrentPosition, ATM
//    ShrinkPrice(Security, price), Security.TickSize, Order.Status() ext.
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ATAS.DataFeedsCore;
using ATAS.DataFeedsCore.Dom;
using ATAS.Indicators;
using ATAS.Strategies.Chart;

namespace ATAS.McpBridge
{
    [DisplayName("MCP Bridge")]
    public class McpBridgeStrategy : ChartStrategy
    {
        // ------------------------------------------------------------------
        //  Constants
        // ------------------------------------------------------------------
        public const int DefaultPort = 8787;
        public const int MaxCandles = 2000;
        private const string BridgeId = "atas-mcp-bridge";
        private const string BridgeVersion = "1.0.0";
        private static readonly string AppDataAtas = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
        private static readonly string LogPath = Path.Combine(AppDataAtas, "McpBridge.log");
        private static readonly string PortPath = Path.Combine(AppDataAtas, "McpBridge.port");

        // ------------------------------------------------------------------
        //  Cached state (written only on the ATAS thread, read from any thread)
        // ------------------------------------------------------------------
        private volatile string _statusJson = "{}";
        private volatile string _quoteJson = "{}";
        private volatile string _domJson = "{}";
        private volatile string _candlesJson = "{}";
        private volatile string _ordersJson = "[]";
        private volatile string _positionJson = "{}";
        private volatile string _tradesJson = "[]";

        private readonly object _candlesLock = new();
        private readonly List<Dictionary<string, object>> _candles = new();
        private int _lastCachedBar = -1;

        private readonly ConcurrentQueue<CommandRequest> _actions = new();

        private readonly object _subsLock = new();
        private readonly List<StreamSubscriber> _subscribers = new();

        private HttpListener _listener;
        private volatile int _actualPort = -1;
        private CancellationTokenSource _serverCts;
        private Task _serverTask;

        private DateTime _lastDomRefresh = DateTime.MinValue;
        private DateTime _lastOrdersRefresh = DateTime.MinValue;
        private DateTime _lastStatusRefresh = DateTime.MinValue;
        private DateTime _lastQuotePush = DateTime.MinValue;
        private DateTime _lastDomPush = DateTime.MinValue;
        private DateTime _lastPositionPush = DateTime.MinValue;
        private DateTime _lastCandleJsonTime = DateTime.MinValue;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // ------------------------------------------------------------------
        //  Constructor / lifecycle
        // ------------------------------------------------------------------
        public McpBridgeStrategy()
            : base(useCandles: true)
        {
            if (DataSeries != null && DataSeries.Count > 0)
                DataSeries[0].IsHidden = true;
        }

        protected override void OnStarted()
        {
            base.OnStarted();
            Log("strategy started");
            StartHttpServer();
            RefreshAll(force: true);
        }

        protected override void OnStopped()
        {
            base.OnStopped();
            StopHttpServer();
            Log("strategy stopped");
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            try
            {
                UpdateCandleCache();
                RefreshQuoteAndDom();
                RefreshOrdersAndPositions();
                UpdateStatus();
                DrainActions();
            }
            catch (Exception ex)
            {
                // Never let a single bad tick/order kill the strategy inside ATAS.
                Log("OnCalculate error: " + ex.Message);
            }
        }

        protected override void OnOrderChanged(Order order)
        {
            base.OnOrderChanged(order);
            try { PushEvent("order", OrderToJson(order)); } catch { }
            RefreshOrdersAndPositions(force: true);
            DrainActions();
        }

        protected override void OnOrderRegisterFailed(Order order, string message)
        {
            base.OnOrderRegisterFailed(order, message);
            try { PushEvent("orderFailed", new { id = order?.Id, message }); } catch { }
            Log("order register failed: " + message);
        }

        // ------------------------------------------------------------------
        //  HTTP server
        // ------------------------------------------------------------------
        private void StartHttpServer()
        {
            if (_listener != null)
                return; // already running

            try
            {
                _serverCts = new CancellationTokenSource();
                _serverTask = Task.Run(() => RunHttpServerAsync(_serverCts.Token));
            }
            catch (Exception ex)
            {
                Log("failed to start server: " + ex);
            }
        }

        private void StopHttpServer()
        {
            try
            {
                _serverCts?.Cancel();
                _listener?.Close();
            }
            catch { }
            finally
            {
                _listener = null;   // allow a clean restart on the next OnStarted
                _actualPort = -1;
                _serverCts?.Dispose();
                _serverCts = null;
            }
        }

        private async Task RunHttpServerAsync(CancellationToken ct)
        {
            var ports = new List<int>();
            var envPort = Environment.GetEnvironmentVariable("ATAS_MCP_PORT");
            if (int.TryParse(envPort, out var ep))
                ports.Add(ep);
            for (int p = DefaultPort; p < DefaultPort + 20; p++)
                ports.Add(p);

            foreach (var port in ports.Distinct())
            {
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    try { listener.Prefixes.Add($"http://127.0.0.1:{port}/"); } catch { }
                    listener.Start();
                    _listener = listener;
                    _actualPort = port;
                    WritePortFile(port);
                    Log($"HTTP bridge listening on http://localhost:{port}/ and http://127.0.0.1:{port}/");
                    break;
                }
                catch (Exception ex)
                {
                    Log($"port {port} unavailable: {ex.Message}");
                }
            }

            if (_listener == null)
            {
                Log("FATAL: could not bind any port (run once: netsh http add urlacl url=http://localhost:8787/ user=everyone)");
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    break; // listener closed
                }

                _ = Task.Run(() => HandleContextAsync(ctx, ct));
            }
        }

        private async Task HandleContextAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                var req = ctx.Request;
                var path = req.Url?.AbsolutePath ?? "/";
                var method = req.HttpMethod ?? "GET";

                if (path == "/api/stream" && method == "GET")
                {
                    await StreamAsync(ctx, ct);
                    return;
                }

                int code = 200;
                string json;

                switch (path)
                {
                    case "/api/health":
                        json = "{\"ok\":true,\"id\":\"" + BridgeId + "\",\"version\":\"" + BridgeVersion + "\"}";
                        break;
                    case "/api/status":
                        json = _statusJson;
                        break;
                    case "/api/quote":
                        json = _quoteJson;
                        break;
                    case "/api/dom":
                        json = _domJson;
                        break;
                    case "/api/candles":
                        json = _candlesJson;
                        break;
                    case "/api/orders":
                        json = _ordersJson;
                        break;
                    case "/api/position":
                        json = _positionJson;
                        break;
                    case "/api/trades":
                        json = _tradesJson;
                        break;
                    case "/api/snapshot":
                        json = SnapshotJson();
                        break;
                    case "/api/log":
                        json = ReadLogTail();
                        break;
                    case "/api/order":
                        if (method != "POST") { json = MethodNotAllowed(); break; }
                        json = await RunActionAsync("place", ReadBody(req));
                        break;
                    case "/api/order/cancel":
                        if (method != "POST") { json = MethodNotAllowed(); break; }
                        json = await RunActionAsync("cancel", ReadBody(req));
                        break;
                    case "/api/position/close":
                        if (method != "POST") { json = MethodNotAllowed(); break; }
                        json = await RunActionAsync("close", ReadBody(req));
                        break;
                    default:
                        code = 404;
                        json = "{\"ok\":false,\"error\":\"not found\"}";
                        break;
                }

                WriteJson(ctx.Response, json, code);
            }
            catch (Exception ex)
            {
                Log("HTTP error: " + ex);
                try { WriteJson(ctx.Response, "{\"ok\":false,\"error\":\"bridge error: " + JsonEscape(ex.Message) + "\"}", 500); }
                catch { }
            }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        private async Task StreamAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var res = ctx.Response;
            res.StatusCode = 200;
            res.ContentType = "text/event-stream";
            res.Headers["Cache-Control"] = "no-cache";
            res.Headers["Connection"] = "keep-alive";
            res.SendChunked = true;

            var sub = new StreamSubscriber();
            lock (_subsLock)
                _subscribers.Add(sub);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (sub.Queue.TryTake(out var line, TimeSpan.FromMilliseconds(200)))
                    {
                        var payload = Encoding.UTF8.GetBytes("data: " + line + "\n\n");
                        await res.OutputStream.WriteAsync(payload, 0, payload.Length, ct);
                        await res.OutputStream.FlushAsync(ct);
                    }
                    else if ((DateTime.UtcNow - sub.LastBeat).TotalSeconds >= 15)
                    {
                        var ping = Encoding.UTF8.GetBytes(": ping\n\n");
                        await res.OutputStream.WriteAsync(ping, 0, ping.Length, ct);
                        await res.OutputStream.FlushAsync(ct);
                        sub.LastBeat = DateTime.UtcNow;
                    }
                }
            }
            catch
            {
                // client disconnected
            }
            finally
            {
                lock (_subsLock)
                    _subscribers.Remove(sub);
            }
        }

        private void PushEvent(string type, object data)
        {
            string line;
            try
            {
                line = JsonSerializer.Serialize(new
                {
                    type,
                    data,
                    ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, JsonOpts);
            }
            catch { return; }

            List<StreamSubscriber> subs;
            lock (_subsLock)
                subs = _subscribers.ToList();

            foreach (var s in subs)
                s.Queue.TryAdd(line);
        }

        private void PushEventThrottled(string type, object data, ref DateTime last, int minMs)
        {
            var now = DateTime.UtcNow;
            if ((now - last).TotalMilliseconds < minMs)
                return;
            last = now;
            PushEvent(type, data);
        }

        // ------------------------------------------------------------------
        //  Action commands (executed on the ATAS thread via OnCalculate)
        // ------------------------------------------------------------------
        private async Task<string> RunActionAsync(string kind, string body)
        {
            var cmd = new CommandRequest
            {
                Kind = kind,
                Body = body,
                Tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            _actions.Enqueue(cmd);

            try
            {
                return await cmd.Tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (TimeoutException)
            {
                return "{\"ok\":false,\"error\":\"timeout - is the MCP Bridge strategy active on a chart?\"}";
            }
        }

        private void DrainActions()
        {
            while (_actions.TryDequeue(out var cmd))
            {
                string result;
                try
                {
                    result = ExecuteAction(cmd.Kind, cmd.Body);
                }
                catch (Exception ex)
                {
                    Log("action failed: " + cmd.Kind + " -> " + ex.Message);
                    result = "{\"ok\":false,\"error\":\"" + JsonEscape(ex.Message) + "\"}";
                }
                cmd.Tcs.TrySetResult(result);
            }
        }

        private string ExecuteAction(string kind, string body)
        {
            if (!IsActivated)
                return Err("strategy not activated - enable IsActivated in ATAS strategy settings");
            if (Security == null)
                return Err("no security on chart");
            if (Portfolio == null)
                return Err("no portfolio selected in ATAS");

            switch (kind)
            {
                case "place":  return PlaceOrder(body);
                case "cancel": return CancelOrderById(body);
                case "close":  return FlattenPosition(body);
                default:       return Err("unknown action: " + kind);
            }
        }

        private string PlaceOrder(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var directionStr = GetStr(root, "direction");
            var typeStr = GetStr(root, "type");
            decimal qty = GetDec(root, "qty");
            decimal price = GetDec(root, "price");
            decimal trigger = GetDec(root, "triggerPrice");

            if (!Enum.TryParse<OrderDirections>(directionStr, true, out var direction))
                return Err("direction must be 'buy' or 'sell'");
            if (!Enum.TryParse<OrderTypes>(typeStr, true, out var type))
                return Err("type must be 'limit', 'stop', 'market' or 'stoplimit'");
            if (qty <= 0)
                return Err("qty must be > 0");

            if (type == OrderTypes.Limit && price <= 0)
                return Err("price is required for limit orders");
            if (type == OrderTypes.Stop && trigger <= 0)
                return Err("triggerPrice is required for stop orders");
            if (type == OrderTypes.StopLimit && (price <= 0 || trigger <= 0))
                return Err("price and triggerPrice are required for stoplimit orders");

            var order = new Order
            {
                Portfolio = Portfolio,
                Security = Security,
                Direction = direction,
                Type = type,
                QuantityToFill = qty,
                Comment = GetStr(root, "comment")
            };

            if (type == OrderTypes.Limit)
                order.Price = ShrinkPrice(price);
            else if (type == OrderTypes.Stop)
                order.TriggerPrice = ShrinkPrice(trigger);
            else if (type == OrderTypes.StopLimit)
            {
                order.TriggerPrice = ShrinkPrice(trigger);
                order.Price = ShrinkPrice(price);
            }
            // market orders: price/trigger ignored by the exchange

            OpenOrder(order);

            var ok = new
            {
                ok = true,
                message = "order sent",
                id = order.Id,
                direction = direction.ToString(),
                type = type.ToString(),
                qty,
                price = type == OrderTypes.Limit || type == OrderTypes.StopLimit ? order.Price : 0m,
                triggerPrice = type == OrderTypes.Stop || type == OrderTypes.StopLimit ? order.TriggerPrice : 0m,
                comment = order.Comment
            };
            PushEvent("orderSent", ok);
            return JsonSerializer.Serialize(ok, JsonOpts);
        }

        private string CancelOrderById(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var id = GetStr(doc.RootElement, "id");
            if (string.IsNullOrWhiteSpace(id))
                return Err("'id' is required");

            if (Orders == null)
                return Err("no orders available");

            var order = Orders.FirstOrDefault(o =>
                string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
            if (order == null)
                return Err("order not found: " + id);

            CancelOrder(order);
            var ok = new { ok = true, message = "cancel sent", id };
            PushEvent("orderCancelSent", ok);
            return JsonSerializer.Serialize(ok, JsonOpts);
        }

        private string FlattenPosition(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var net = GetNetPosition();
            if (net.Volume == 0m)
                return Err("no current position to close");

            decimal volume = GetDec(root, "volume");
            if (volume <= 0m)
                volume = net.Volume;

            var closeDirection = net.Direction == OrderDirections.Buy
                ? OrderDirections.Sell
                : OrderDirections.Buy;

            var order = new Order
            {
                Portfolio = Portfolio,
                Security = Security,
                Direction = closeDirection,
                Type = OrderTypes.Market,
                QuantityToFill = volume,
                Comment = "mcp-bridge-flatten"
            };

            OpenOrder(order);
            var ok = new
            {
                ok = true,
                message = "flatten market order sent",
                volume,
                direction = closeDirection.ToString()
            };
            PushEvent("flattenSent", ok);
            return JsonSerializer.Serialize(ok, JsonOpts);
        }

        // ------------------------------------------------------------------
        //  Cache refresh (always on the ATAS thread)
        // ------------------------------------------------------------------
        private void RefreshAll(bool force)
        {
            UpdateCandleCache();
            RefreshQuoteAndDom(force);
            RefreshOrdersAndPositions(force);
            UpdateStatus(force);
        }

        private void UpdateCandleCache()
        {
            int end = CurrentBar; // index of the forming candle
            if (end < 0)
                return;

            lock (_candlesLock)
            {
                int start = _lastCachedBar + 1;
                if (start < 0) start = 0;

                bool newBar = start < end;
                if (newBar)
                {
                    for (int i = start; i < end; i++)
                    {
                        try { _candles.Add(CandleToJson(GetCandle(i))); } catch { }
                    }
                    if (_candles.Count > MaxCandles)
                        _candles.RemoveRange(0, _candles.Count - MaxCandles);
                    _lastCachedBar = end - 1;
                }

                // Rebuild the JSON only when a bar completed or at most 1x/second,
                // so we never serialize 2000 candles on every tick.
                if (newBar || (DateTime.UtcNow - _lastCandleJsonTime).TotalMilliseconds >= 1000)
                {
                    Dictionary<string, object> current = null;
                    try { current = CandleToJson(GetCandle(end)); } catch { } // forming candle (may not exist yet)

                    _candlesJson = JsonSerializer.Serialize(new { candles = _candles, current }, JsonOpts);
                    _lastCandleJsonTime = DateTime.UtcNow;
                }
            }
        }

        private static Dictionary<string, object> CandleToJson(IndicatorCandle c)
        {
            return new Dictionary<string, object>
            {
                ["open"] = c.Open,
                ["high"] = c.High,
                ["low"] = c.Low,
                ["close"] = c.Close,
                ["volume"] = c.Volume,
                ["delta"] = c.Delta,
                ["time"] = c.Time.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private void RefreshQuoteAndDom(bool force = false)
        {
            var now = DateTime.UtcNow;
            if (!force && (now - _lastDomRefresh).TotalMilliseconds < 300)
                return;

            decimal last = 0m;
            try { last = GetCandle(CurrentBar - 1).Close; } catch { }

            // Best bid/ask come straight from the strategy (MarketDataArg)
            decimal bestBid = 0m, bestAsk = 0m, bidSize = 0m, askSize = 0m;
            try
            {
                if (BestBid != null) { bestBid = BestBid.Price; bidSize = BestBid.Volume; }
                if (BestAsk != null) { bestAsk = BestAsk.Price; askSize = BestAsk.Volume; }
            }
            catch { }

            // Full DOM from MarketDepthInfo
            var bids = new List<Dictionary<string, object>>();
            var asks = new List<Dictionary<string, object>>();
            decimal cumBids = 0m, cumAsks = 0m;
            try
            {
                var snap = MarketDepthInfo.GetMarketDepthSnapshot();
                foreach (var d in snap)
                {
                    var lvl = new Dictionary<string, object>
                    {
                        ["price"] = d.Price,
                        ["volume"] = d.Volume,
                        ["side"] = d.IsBid ? "Bid" : d.IsAsk ? "Ask" : "Trade",
                        ["time"] = d.Time.ToString("O", CultureInfo.InvariantCulture)
                    };
                    if (d.IsBid) bids.Add(lvl);
                    else if (d.IsAsk) asks.Add(lvl);
                }
                cumBids = MarketDepthInfo.CumulativeDomBids;
                cumAsks = MarketDepthInfo.CumulativeDomAsks;
            }
            catch { }

            bids = bids.OrderByDescending(x => GetDecimal(x, "price")).ToList();
            asks = asks.OrderBy(x => GetDecimal(x, "price")).ToList();

            var quote = new Dictionary<string, object>
            {
                ["last"] = last,
                ["bestBid"] = bestBid,
                ["bestAsk"] = bestAsk,
                ["bidSize"] = bidSize,
                ["askSize"] = askSize,
                ["cumBids"] = cumBids,
                ["cumAsks"] = cumAsks,
                ["spread"] = bestBid > 0m && bestAsk > 0m ? bestAsk - bestBid : 0m,
                ["tickSize"] = TryTickSize(),
                ["time"] = now.ToString("O", CultureInfo.InvariantCulture)
            };
            _quoteJson = JsonSerializer.Serialize(quote, JsonOpts);

            var dom = new Dictionary<string, object>
            {
                ["bids"] = bids,
                ["asks"] = asks,
                ["time"] = now.ToString("O", CultureInfo.InvariantCulture)
            };
            _domJson = JsonSerializer.Serialize(dom, JsonOpts);
            _lastDomRefresh = now;

            PushEventThrottled("quote", quote, ref _lastQuotePush, 200);
            PushEventThrottled("dom", dom, ref _lastDomPush, 500);
        }

        private void RefreshOrdersAndPositions(bool force = false)
        {
            var now = DateTime.UtcNow;
            if (!force && (now - _lastOrdersRefresh).TotalMilliseconds < 500)
                return;

            var orders = new List<object>();
            if (Orders != null)
            {
                foreach (var o in Orders)
                    orders.Add(OrderToJson(o));
            }
            _ordersJson = JsonSerializer.Serialize(orders, JsonOpts);

            _positionJson = JsonSerializer.Serialize(GetPositionJson(), JsonOpts);

            var trades = new List<object>();
            if (MyTrades != null)
            {
                foreach (var t in MyTrades)
                    trades.Add(TradeToJson(t));
            }
            _tradesJson = JsonSerializer.Serialize(trades, JsonOpts);

            _lastOrdersRefresh = now;

            PushEventThrottled("position", GetPositionJson(), ref _lastPositionPush, 1000);
        }

        // ------------------------------------------------------------------
        //  Serialization helpers (verified against ATAS 7.x API)
        // ------------------------------------------------------------------
        private static Dictionary<string, object> OrderToJson(Order o)
        {
            var d = new Dictionary<string, object>
            {
                ["id"] = o.Id,
                ["direction"] = o.Direction.ToString(),
                ["type"] = o.Type.ToString(),
                ["state"] = o.State.ToString(),
                ["status"] = o.Status().ToString(),
                ["price"] = o.Price,
                ["triggerPrice"] = o.TriggerPrice,
                ["qty"] = o.QuantityToFill,
                ["unfilled"] = o.Unfilled,
                ["filled"] = Math.Max(0m, o.QuantityToFill - o.Unfilled),
                ["comment"] = o.Comment,
                ["account"] = o.AccountID,
                ["symbol"] = o.Security?.Code,
                ["time"] = o.Time.ToString("O", CultureInfo.InvariantCulture)
            };
            return d;
        }

        private static Dictionary<string, object> TradeToJson(MyTrade t)
        {
            return new Dictionary<string, object>
            {
                ["id"] = t.Id,
                ["orderId"] = t.OrderId,
                ["price"] = t.Price,
                ["volume"] = t.Volume,
                ["direction"] = t.OrderDirection.ToString(),
                ["symbol"] = t.Security?.Code,
                ["time"] = t.Time.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        // Position object is not exposed on ChartStrategy in ATAS 7.x; we derive
        // the net position from MyTrades (fills) and use the strategy PnL fields.
        private (OrderDirections Direction, decimal Volume) GetNetPosition()
        {
            decimal net = 0m;
            if (MyTrades != null)
            {
                foreach (var t in MyTrades)
                    net += t.OrderDirection == OrderDirections.Buy ? t.Volume : -t.Volume;
            }
            if (net > 0m)
                return (OrderDirections.Buy, net);
            if (net < 0m)
                return (OrderDirections.Sell, -net);
            return (OrderDirections.Buy, 0m);
        }

        private Dictionary<string, object> GetPositionJson()
        {
            var net = GetNetPosition();
            return new Dictionary<string, object>
            {
                ["inPosition"] = net.Volume > 0m,
                ["direction"] = net.Volume > 0m ? net.Direction.ToString() : null,
                ["volume"] = net.Volume,
                ["averagePrice"] = AveragePrice,
                ["openPnL"] = OpenPnL,
                ["closedPnL"] = ClosedPnL,
                ["currentPosition"] = CurrentPosition, // unrealized PnL in currency
                ["symbol"] = Security?.Code,
                ["time"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private void UpdateStatus(bool force = false)
        {
            var now = DateTime.UtcNow;
            if (!force && (now - _lastStatusRefresh).TotalMilliseconds < 1000)
                return;

            _statusJson = BuildStatus();
            _lastStatusRefresh = now;
        }

        private string BuildStatus()
        {
            var d = new Dictionary<string, object>
            {
                ["id"] = BridgeId,
                ["version"] = BridgeVersion,
                ["port"] = _actualPort,
                ["symbol"] = Security?.Code,
                ["portfolio"] = Portfolio?.AccountID,
                ["instrument"] = Security?.Instrument,
                ["tickSize"] = TryTickSize(),
                ["activated"] = IsActivated,
                ["currentBar"] = CurrentBar,
                ["state"] = State.ToString(),
                ["time"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            return JsonSerializer.Serialize(d, JsonOpts);
        }

        private string SnapshotJson()
        {
            var d = new Dictionary<string, object>
            {
                ["status"] = ParseOrNull(_statusJson),
                ["quote"] = ParseOrNull(_quoteJson),
                ["dom"] = ParseOrNull(_domJson),
                ["candles"] = ParseOrNull(_candlesJson),
                ["orders"] = ParseOrNull(_ordersJson),
                ["position"] = ParseOrNull(_positionJson),
                ["trades"] = ParseOrNull(_tradesJson)
            };
            return JsonSerializer.Serialize(d, JsonOpts);
        }

        private static object ParseOrNull(string json)
        {
            try { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
            catch { return null; }
        }

        // ------------------------------------------------------------------
        //  Small helpers
        // ------------------------------------------------------------------
        private decimal TryTickSize()
        {
            try { return Security?.TickSize ?? 0m; } catch { return 0m; }
        }

        private static string GetStr(JsonElement el, string name)
        {
            return el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? ""
                : "";
        }

        private static decimal GetDec(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var v))
                return 0m;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
                return d;
            return decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2) ? d2 : 0m;
        }

        private static decimal GetDecimal(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var v) || v == null)
                return 0m;
            return v is decimal d ? d :
                   v is int i ? i :
                   v is long l ? l :
                   v is double db ? (decimal)db :
                   v is float f ? (decimal)f :
                   decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2) ? d2 : 0m;
        }

        private static string Err(string message)
        {
            return "{\"ok\":false,\"error\":\"" + JsonEscape(message) + "\"}";
        }

        private static string MethodNotAllowed()
        {
            return "{\"ok\":false,\"error\":\"method not allowed\"}";
        }

        private static string JsonEscape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        }

        private static void WriteJson(HttpListenerResponse res, string json, int code = 200)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            res.StatusCode = code;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private static string ReadBody(HttpListenerRequest req)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static string ReadLogTail()
        {
            try
            {
                if (!File.Exists(LogPath))
                    return "{\"log\":[]}";
                var lines = File.ReadAllLines(LogPath);
                var tail = lines.Skip(Math.Max(0, lines.Length - 100)).ToList();
                return JsonSerializer.Serialize(new { log = tail }, JsonOpts);
            }
            catch { return "{\"log\":[]}"; }
        }

        private static void Log(string message)
        {
            try
            {
                if (!Directory.Exists(AppDataAtas))
                    Directory.CreateDirectory(AppDataAtas);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1_000_000)
                    File.WriteAllText(LogPath, line); // keep the log small
                File.AppendAllText(LogPath, line);
            }
            catch { }
        }

        private static void WritePortFile(int port)
        {
            try
            {
                if (!Directory.Exists(AppDataAtas))
                    Directory.CreateDirectory(AppDataAtas);
                File.WriteAllText(PortPath, port.ToString());
            }
            catch { }

            try
            {
                var appDataAtasX = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS X");
                if (Directory.Exists(appDataAtasX))
                    File.WriteAllText(Path.Combine(appDataAtasX, "McpBridge.port"), port.ToString());
            }
            catch { }
        }

        // ------------------------------------------------------------------
        //  Nested types
        // ------------------------------------------------------------------
        private sealed class CommandRequest
        {
            public string Kind;
            public string Body;
            public TaskCompletionSource<string> Tcs;
        }

        private sealed class StreamSubscriber
        {
            public readonly BlockingCollection<string> Queue = new(100);
            public DateTime LastBeat = DateTime.UtcNow;
        }
    }
}
