# ATAS MCP Bridge - Arquitectura y Especificacion Tecnica

Documento de referencia tecnica y diseno de arquitectura del sistema.
Estado: Addon compilado y desplegado, servidor MCP validado y verificado.

---

## 1. Proposito del Proyecto

ATAS MCP Bridge proporciona un puente de comunicacion bidireccional entre la plataforma de trading ATAS (Advanced Time And Sales Platform) y agentes autonomos basados en Model Context Protocol (MCP).

Permite que modelos de lenguaje avanzados (LLMs) y asistentes de desarrollo accedan en tiempo real a:
- Ticks y cotizaciones bid/ask con desbalance de volumen.
- Libro de ordenes en profundidad (DOM / Depth of Market / Order Flow).
- Hasta 2000 velas historicas con delta de volumen por barra y vela en formacion.
- Estado de la cuenta, ordenes abiertas, ejecuciones (fills) y posicion neta.
- Ejecucion de ordenes (Limit, Stop, Market, StopLimit), cancelacion y cierre de posicion (flatten).

---

## 2. Diagrama de Arquitectura del Sistema

```
+---------------------------------------------------------------------------------------------+
|                                    PROCESO ATAS PLATFORM                                    |
|                                                                                             |
|  +---------------------------------------------------------------------------------------+  |
|  |                     McpBridgeStrategy (C# / .NET 8 / ATAS 7.x & 8.x)                  |  |
|  |                                                                                       |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  |  |                         Hilo Principal de Calculo (ATAS Engine)                 |  |  |
|  |  |                                                                                 |  |  |
|  |  |  OnCalculate() / OnOrderChanged()                                               |  |  |
|  |  |   1. Procesa ticks del mercado                                                  |  |  |
|  |  |   2. Actualiza snapshots JSON volatiles (_quoteJson, _domJson, _candlesJson)    |  |  |
|  |  |   3. Desencola peticiones de accion (DrainActions) y ejecuta ordenes de trading |  |  |
|  |  |   4. Notifica eventos al motor de streaming SSE (PushEvent)                     |  |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  |                                                                                       |  |
|  |  +-------------------------------------+     +-------------------------------------+  |  |
|  |  |      ConcurrentQueue<CommandRequest>|     |     Snapshots JSON Volatiles        |  |  |
|  |  |  Cola hilo-segura de ordenes        |     |  Lecturas ultra-rapidas lock-free   |  |  |
|  |  +-------------------------------------+     +-------------------------------------+  |  |
|  |                     ^                                           |                     |  |
|  |                     | Encolado                                  | Lectura             |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  |  |                      Servidor HTTP / SSE Embebido (HttpListener)                |  |  |
|  |  |                      Puerto: 127.0.0.1:8787 (o rango 8787..8807)               |  |  |
|  |  +---------------------------------------------------------------------------------+  |  |
|  +---------------------------------------------------------------------------------------+  |
+---------------------------------------------------------------------------------------------+
                                       ^
                                       | HTTP REST / SSE (localhost:8787)
                                       v
+---------------------------------------------------------------------------------------------+
|                                   CAPA MCP SERVER (PYTHON)                                  |
|                                                                                             |
|  +-----------------------------------+         +-----------------------------------------+  |
|  |           atas_client.py          | <-----> |                server.py                |  |
|  |  - Cliente HTTP nativo (urllib)   |         |  - Servidor MCP (mcp 2.0 MCPServer)     |  |
|  |  - Deteccion de puerto dinamico   |         |  - 12 herramientas registradas          |  |
|  |  - Conversion y blindaje de errores|         |  - Control de excepciones estructurado  |  |
|  +-----------------------------------+         +-----------------------------------------+  |
+---------------------------------------------------------------------------------------------+
                                       ^
                                       | MCP Protocol (stdio / JSON-RPC)
                                       v
+---------------------------------------------------------------------------------------------+
|                                      CLIENTES DE IA                                         |
|                                                                                             |
|   Claude Desktop  |  Cursor IDE  |  Claude Code CLI  |  Antigravity CLI  |  OpenClaw / Bot  |
+---------------------------------------------------------------------------------------------+
```

---

## 3. Componentes y Responsabilidades

### A. Addon C# (ATAS.McpBridge)
- **Ruta del codigo:** `ATAS.McpBridge/McpBridgeStrategy.cs`
- **Tipo:** `ChartStrategy` (hereda de `ATAS.Strategies.Chart.ChartStrategy`).
- **Runtime:** .NET 8 (`net8.0-windows`), compatible tanto con ATAS 7.x como con ATAS X (8.x).
- **Dependencias externas:** 0. Solo utiliza librerias base del framework .NET (`System.Net.HttpListener`, `System.Text.Json`, `System.Collections.Concurrent`).
- **Ensamblados referenciados de ATAS:**
  - `ATAS.DataFeedsCore.dll`
  - `ATAS.Indicators.dll`
  - `ATAS.Strategies.dll`
  - `ATAS.Types.dll`
  - `Utils.Common.dll` (resuelve interfaces comunes de logging).

### B. Servidor MCP Python (server.py)
- Implementado utilizando el SDK oficial de MCP (`mcp>=1.2.0`, `MCPServer`).
- Expone 12 herramientas `atas_*` sobre entrada/salida estandar (`stdio`).
- Implementa la funcion envoltorio `_guard()` que intercepta cualquier excepcion del cliente HTTP o de red, devolviendo un objeto estructurado `{"ok": false, "error": "..."}` en lugar de abortar el proceso.

### C. Cliente HTTP Python (atas_client.py)
- Libreria de comunicacion sin dependencias de terceros (utiliza exclusivamente `urllib.request` y `json`).
- Implementa la funcion `_discover_port()` que:
  1. Comprueba la variable de entorno `ATAS_MCP_PORT`.
  2. Lee el archivo de puerto en `%APPDATA%\ATAS\McpBridge.port`.
  3. Lee el archivo de puerto en `%APPDATA%\ATAS X\McpBridge.port`.
  4. Utiliza el puerto por defecto 8787 si ninguno de los anteriores esta disponible.
- Se comunica por defecto a traves de `127.0.0.1` para evitar retrasos de resolucion IPv6 en Windows.

---

## 4. Endpoints HTTP del Addon

| Endpoint | Metodo | Parametros | Respuesta |
|---|---|---|---|
| `/api/health` | GET | Ninguno | `{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}` |
| `/api/status` | GET | Ninguno | Metadatos de conexion, simbolo, portfolio, tick size, estado activado y puerto |
| `/api/quote` | GET | Ninguno | Ultimo precio, bid/ask, spread, volumen acumulado y tick size |
| `/api/dom` | GET | Ninguno | Niveles ordenados de compra y venta del book con precio y tamano |
| `/api/candles` | GET | Ninguno | Arreglo de hasta 2000 velas cerradas mas la vela en curso |
| `/api/orders` | GET | Ninguno | Lista de ordenes activas con cantidades ejecutadas y pendientes |
| `/api/position` | GET | Ninguno | Volumen neto, direccion, PnL abierto y cerrado |
| `/api/trades` | GET | Ninguno | Ejecuciones (fills) completadas durante la sesion |
| `/api/snapshot` | GET | Ninguno | Objeto consolidado con todas las metricas de mercado y cuenta |
| `/api/log` | GET | Ninguno | Ultimas lineas del registro de diagnostico interno |
| `/api/order` | POST | `direction`, `type`, `qty`, `price?`, `triggerPrice?`, `comment?` | Confirmacion de envio y metadata de la orden |
| `/api/order/cancel` | POST | `id` | Confirmacion de solicitud de cancelacion enviada |
| `/api/position/close` | POST | `volume?`, `direction?` | Confirmacion de orden de mercado de cierre (flatten) |
| `/api/stream` | GET (SSE) | Ninguno | Flujo continuo de eventos Server-Sent Events |

---

## 5. Especificacion de las 12 Herramientas MCP

1. `atas_status`: Estado general del puente y la conexion con ATAS.
2. `atas_quote`: Datos de cotizacion en vivo y desbalance del libro.
3. `atas_dom(levels: int = 15)`: Profundidad de mercado filtrada por numero de niveles por lado.
4. `atas_candles`: Serie historica de barras OHLCV con delta.
5. `atas_position`: Resumen de posicion abierta y rendimiento de la sesion.
6. `atas_orders`: Inspeccion de ordenes pendientes de ejecucion.
7. `atas_trades`: Lista de transacciones concluidas en la sesion.
8. `atas_snapshot`: Vision global instantanea en una sola llamada de herramienta.
9. `atas_place_order(direction, order_type, qty, price?, trigger_price?, comment?)`: Envio de orden de compra o venta.
10. `atas_cancel_order(order_id)`: Cancelacion de orden por identificador unico.
11. `atas_close_position(volume?, direction?)`: Cierre de posicion mediante orden a mercado.
12. `atas_bridge_log`: Recuperacion de lineas de log de diagnostico generadas por el addon.

---

## 6. Particularidades Tecnicas de la API de ATAS 7.x vs 8.x

A traves de la inspeccion de metadatos de los ensamblados DLL de ATAS en el equipo local se establecieron las siguientes directrices criticas:

| Concepto | ATAS 8.x / Documentacion Online | ATAS 7.x (Instalacion Local) | Tratamiento en el Bridge |
|---|---|---|---|
| Tipo devuelto por `GetCandle()` | `Candle` | `IndicatorCandle` | Se utiliza `IndicatorCandle`, el cual contiene `Volume`, `Delta`, `Time`, `Open`, `High`, `Low`, `Close`. |
| Propiedad `CurrentPosition` | Objeto complejo `Position` | `decimal` (PnL sin realizar) | Se lee como `decimal`. La posicion neta (volumen y direccion) se calcula iterando `MyTrades`. |
| Normalizacion de precios | Metodo estatico externo | Metodo protegido heredado `ShrinkPrice(price)` | Se invoca `ShrinkPrice(price)` directamente en la clase base `ChartStrategy`. |
| Datos del instrumento | `InstrumentInfo` | Propiedades en `Security` (`TickSize`, `Instrument`, etc.) | Se lee directamente desde `Security`. |
| Mejor Bid / Mejor Ask | `MarketDataArg` | `MarketDataArg` en namespace `ATAS.Indicators` | Se obtiene mediante `BestBid` y `BestAsk`. |
| Libro de profundidad (DOM) | Eventos separados | `MarketDepthInfo.GetMarketDepthSnapshot()` | Se toma la coleccion de niveles de `MarketDepthInfo` y se clasifica por `IsBid` / `IsAsk`. |
| Estado de orden | Propiedad de enum | Metodo de extension `Order.Status()` | Requiere inclusion de namespace `ATAS.DataFeedsCore`. |
| Ciclo de ejecucion | `base.OnCalculate()` | Metodo abstracto | No se debe invocar `base.OnCalculate()` en `ChartStrategy`. |

---

## 7. Diseno de Concurrencia y Seguridad

1. **Aislamiento del Hilo de ATAS:**
   - La API de ATAS no es hilo-segura para llamadas simultaneas desde hilos externos.
   - Las solicitudes HTTP de modificacion (`/api/order`, `/api/order/cancel`, `/api/position/close`) se encapsulan en objetos `CommandRequest` con una promesa `TaskCompletionSource<string>` y se colocan en una `ConcurrentQueue`.
   - Cuando el motor de ATAS invoca `OnCalculate()` en cada tick del mercado o cambio de orden, se llama a `DrainActions()`, ejecutando las ordenes de forma nativa en el hilo seguro de la plataforma y completando la promesa asincrona hacia la solicitud HTTP.

2. **Lecturas no bloqueantes (Lock-Free Snapshots):**
   - Durante `OnCalculate()`, se serializan cadenas JSON volatiles (`_statusJson`, `_quoteJson`, `_domJson`, etc.).
   - Cuando un hilo de `HttpListener` procesa una peticion GET, lee directamente la referencia atomica a la cadena JSON en memoria volatil, sin bloquear el procesamiento de ticks de trading.

3. **Autodeteccion y Reintento de Puertos:**
   - En el arranque, el servidor HTTP intenta escuchar en el puerto 8787. Si esta ocupado, prueba de forma incremental hasta el 8807.
   - El puerto final seleccionado se registra en `%APPDATA%\ATAS\McpBridge.port` y `%APPDATA%\ATAS X\McpBridge.port`.

---

## 8. Verificacion y Estado de Pruebas

- **Compilacion .NET:** 0 errores, 0 advertencias (compilado en modo Release para .NET 8).
- **Despliegue automatico:** DLL copiado en `%APPDATA%\ATAS\Strategies\` y `%APPDATA%\ATAS X\Strategies\`.
- **Smoke Test automatizado:** Suite `test_client.py` con 13 pruebas automatizadas completadas exitosamente en < 1 segundo.
- **Validacion de servidor MCP:** Servidor Python validado con SDK de MCP 2.0 y registro verificado de las 12 herramientas.
- **Configuraciones de clientes:** Registrado en Cursor (`.cursor/mcp.json`) y Antigravity (`.gemini/config/mcp_config.json`).
