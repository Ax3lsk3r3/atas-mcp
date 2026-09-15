# ATAS MCP Bridge

Servidor MCP (Model Context Protocol) para conectar ATAS Platform (7.x) y ATAS X (8.x) con agentes de Inteligencia Artificial (Claude Desktop, Cursor, Claude Code, Antigravity CLI, Windsurf, OpenClaw).

Este proyecto expone datos de mercado en tiempo real (cotizaciones, DOM / order flow, velas OHLCV con delta, posiciones, ordenes activas, operaciones ejecutadas) y capacidades completas de ejecucion y gestion de ordenes hacia cualquier cliente compatible con MCP.

---

## Arquitectura General

```
+------------------------------------+          HTTP / SSE (localhost:8787)         +----------------------+
|           ATAS Platform            | <------------------------------------------> |   MCP Server (Py)    |
|            "MCP Bridge"            |             GET  /api/*                      |      server.py       |
|    (ChartStrategy en C# / .NET)    |             POST /api/order                  |      (mcp 2.0)       |
+------------------------------------+             GET  /api/stream                 +----------------------+
                                                                                               ^
                                                                                               | MCP (stdio)
                                                                                               v
                                                                                    +----------------------+
                                                                                    |   Clientes de IA     |
                                                                                    |   - Cursor           |
                                                                                    |   - Claude Desktop   |
                                                                                    |   - Claude Code      |
                                                                                    |   - Antigravity      |
                                                                                    |   - OpenClaw         |
                                                                                    +----------------------+
```

### Flujo de comunicacion:
1. **Addon C# (ATAS.McpBridge):** Se ejecuta dentro del proceso de ATAS como una `ChartStrategy`. Inicia un listener HTTP/SSE local (`127.0.0.1:8787`).
2. **Seguridad de hilos (Thread-Safety):** Todas las lecturas y escrituras hacia los objetos de ATAS ocurren exclusivamente en el hilo principal de ATAS (dentro de `OnCalculate`). Las peticiones HTTP entrantes se encolan mediante `ConcurrentQueue` y son procesadas en el siguiente ciclo de calculo/tick. Las consultas de lectura se resuelven de inmediato leyendo snapshots JSON en memoria volatil, sin bloquear el motor de trading.
3. **Servidor MCP Python (server.py):** Expone 12 herramientas estandarizadas mediante stdio utilizando la especificacion oficial de Model Context Protocol.
4. **Cliente HTTP Python (atas_client.py):** Modulo ligero sin dependencias externas (utiliza unicamente la libreria estandar de Python) con autodeteccion de puerto dinamico y tolerancia a fallos.

---

## Herramientas MCP Disponibles (12 Tools)

| Herramienta | Descripcion | Parametros |
|---|---|---|
| `atas_status` | Verifica la conexion con el bridge: simbolo, portfolio, tick size, estado activado, puerto y connector. | Ninguno |
| `atas_quote` | Cotizacion en vivo: ultimo precio operado, mejor bid y ask con cantidades, desbalance del DOM (volumen acumulado bid/ask), spread y tick size. | Ninguno |
| `atas_dom` | Libro de ordenes en profundidad (DOM): niveles ordenados de compra (descendente) y venta (ascendente) con precio y volumen. | `levels` (int, default: 15) |
| `atas_candles` | Historial de velas (hasta 2000 barras) del grafico donde reside la estrategia: apertura, maximo, minimo, cierre, volumen, delta y marca de tiempo, mas la vela en formacion. | Ninguno |
| `atas_position` | Estado detallado de la posicion abierta: volumen neto, precio promedio, direccion (Buy/Sell), PnL abierto y PnL cerrado. | Ninguno |
| `atas_orders` | Listado de todas las ordenes de trabajo activas: ID, direccion, tipo, precio, precio de disparo, cantidad ejecutada, cantidad pendiente y estado. | Ninguno |
| `atas_trades` | Registro de ejecuciones (fills) completadas durante la sesion actual. | Ninguno |
| `atas_snapshot` | Retorna en una sola llamada el conjunto consolidado: status, quote, dom, candles, orders, position y trades. | Ninguno |
| `atas_place_order` | Envia una orden al mercado a traves de ATAS. El precio se ajusta automaticamente al tick size del activo. | `direction` ("buy"/"sell"), `order_type` ("limit"/"stop"/"market"/"stoplimit"), `qty` (float), `price` (float opcional), `trigger_price` (float opcional), `comment` (string opcional) |
| `atas_cancel_order` | Cancela una orden activa identificada por su ID unico. | `order_id` (string) |
| `atas_close_position` | Cierra (flatten) la posicion actual mediante una orden a mercado en sentido opuesto por el volumen total o parcial indicado. | `volume` (float opcional), `direction` (string opcional) |
| `atas_bridge_log` | Recupera los ultimos registros de diagnostico generados por el addon dentro de ATAS para facilitar depuracion. | Ninguno |

---

## Endpoints HTTP y SSE del Bridge

El addon expone los siguientes endpoints REST y eventos SSE en `http://127.0.0.1:8787`:

| Endpoint | Metodo | Descripcion |
|---|---|---|
| `/api/health` | GET | Comprobacion rapida de disponibilidad (`{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}`). |
| `/api/status` | GET | Metadatos de la plataforma, grafico, instrumento y cuenta conectada. |
| `/api/quote` | GET | Snapshot de cotizacion actual, spread y volumen acumulado del DOM. |
| `/api/dom` | GET | Profundidad de mercado completa (bids y asks ordenados con precio y volumen). |
| `/api/candles` | GET | Array de hasta 2000 velas completadas mas vela actual en tiempo real. |
| `/api/orders` | GET | Lista de ordenes activas en el sistema. |
| `/api/position` | GET | Resumen de la posicion neta calculada a partir de los fills de la estrategia. |
| `/api/trades` | GET | Registro de ejecuciones de la sesion. |
| `/api/snapshot` | GET | Consolidado total de estado, cotizacion, DOM, velas, ordenes y posicion. |
| `/api/log` | GET | Lineas de registro y diagnostico del addon. |
| `/api/order` | POST | Envio de nueva orden (`{"direction":"buy","type":"limit","qty":1,"price":22000.25}`). |
| `/api/order/cancel` | POST | Cancelacion de orden existente (`{"id":"<order_id>"}`). |
| `/api/position/close` | POST | Cierre inmediato de posicion a mercado (`{}`). |
| `/api/stream` | GET (SSE) | Server-Sent Events en tiempo real: eventos `quote` (200ms throttle), `dom` (500ms throttle), `position` (1000ms throttle), `orderSent`, `orderFailed`, `orderCancelSent`, `flattenSent`. |

---

## Requisitos del Sistema

- **Sistema Operativo:** Windows 10 / 11 (x64)
- **ATAS:** ATAS Platform (7.x) o ATAS X (8.x) instalado.
- **SDK de .NET:** .NET 8 SDK o superior (comprobar con `dotnet --version`).
- **Python:** Python 3.10 o superior (con pip).

---

## Guia de Instalacion y Puesta en Marcha

### Paso 1: Instalar dependencias de Python
Abre una terminal en el directorio del proyecto:

```bash
cd C:\Users\ax3lsk3r3\Desktop\atas-mcp
pip install -r requirements.txt
```

### Paso 2: Compilar y desplegar el addon C#
Ejecuta el script de construccion:

```bash
build.bat
```

Este script:
1. Compila `ATAS.McpBridge.csproj` en modo Release utilizando el SDK de .NET.
2. Despliega automaticamente `ATAS.McpBridge.dll` en las carpetas de estrategias correspondientes:
   - `%APPDATA%\ATAS\Strategies\` (ATAS Platform clasico)
   - `%APPDATA%\ATAS X\Strategies\` (ATAS X)

### Paso 3: Activar la estrategia dentro de ATAS
1. Inicia ATAS Platform o ATAS X.
2. Abre un grafico con el instrumento financiero que desees operar (por ejemplo: NQ, ES, BTCUSDT, EURUSD).
3. Haz clic derecho sobre el grafico -> selecciona **Indicators** (o Agregar Indicador).
4. En el buscador escribe **MCP Bridge** y anadelo al grafico.
5. En la ventana de configuracion lateral de la estrategia:
   - Marca la casilla **`IsActivated`**.
   - Selecciona tu cuenta o portfolio (se recomienda una cuenta demo/simulacion).
6. Verifica en tu navegador web que el bridge responde correctamente:
   - `http://127.0.0.1:8787/api/health`
   - Debe responder: `{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}`

### Paso 4: Ejecutar el test de verificacion (Smoke Test)
Puedes validar el funcionamiento del cliente HTTP y el protocolo sin necesidad de tener ATAS abierto:

```bash
python test_client.py
```

Debe mostrar `ALL TESTS PASSED`.

### Paso 5: Arrancar el servidor MCP
Para lanzar el servidor en modo stdio:

```bash
start_mcp.bat
```

---

## Configuracion en Clientes de IA

### Cursor
Edita `%USERPROFILE%\.cursor\mcp.json` y anade:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "C:/Users/ax3lsk3r3/Desktop/atas-mcp/server.py"
      ]
    }
  }
}
```

### Claude Desktop
Edita `%APPDATA%\Claude\claude_desktop_config.json` y anade:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "C:\\Users\\ax3lsk3r3\\Desktop\\atas-mcp\\server.py"
      ],
      "cwd": "C:\\Users\\ax3lsk3r3\\Desktop\\atas-mcp"
    }
  }
}
```

### Claude Code (CLI)
Ejecuta en consola:

```bash
claude mcp add atas -- python "C:\Users\ax3lsk3r3\Desktop\atas-mcp\server.py"
```

### Antigravity
Configura en `%USERPROFILE%\.gemini\config\mcp_config.json`:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "C:/Users/ax3lsk3r3/Desktop/atas-mcp/server.py"
      ]
    }
  }
}
```

Consulta `mcp-config-examples.md` para detalles adicionales y configuraciones para Windsurf y VS Code Cline.

---

## Estructura del Repositorio

```
atas-mcp/
├── ATAS.McpBridge/              # Codigo fuente del Addon C# para ATAS
│   ├── ATAS.McpBridge.csproj    # Definicion del proyecto .NET (referencias a ensamblados ATAS)
│   └── McpBridgeStrategy.cs     # ChartStrategy, servidor HTTP, motor SSE y cola concurrente
├── server.py                    # Servidor MCP stdio con registro de 12 tools atas_*
├── atas_client.py               # Cliente HTTP en Python nativo (sin dependencias externas)
├── test_client.py               # Suite de pruebas automatizadas contra mock HTTP
├── build.bat                    # Script de compilacion y despliegue a carpetas AppData
├── start_mcp.bat                # Script de inicio rapido del servidor MCP
├── install_sdk.bat              # Script auxiliar para instalacion del .NET 8 SDK
├── requirements.txt             # Dependencias Python (mcp>=1.2.0)
├── mcp-config-examples.md       # Plantillas de configuracion para todos los clientes
├── ARQUITECTURA.md              # Documentacion tecnica detallada de la arquitectura interna
└── README.md                    # Documentacion principal del proyecto
```

---

## Detalles Tecnicos y Compatibilidad con la API de ATAS

1. **Version de API de ATAS:**
   - Verificada contra ensamblados nativos de ATAS 7.x (`ATAS.Strategies.dll`, `ATAS.Indicators.dll`, `ATAS.DataFeedsCore.dll`).
   - Uso de `IndicatorCandle` (propiedades `Volume`, `Delta`, `Time`, `Open`, `High`, `Low`, `Close`).
   - Lectura de profundidad mediante `MarketDepthInfo.GetMarketDepthSnapshot()`.
   - Normalizacion de precios con `ShrinkPrice(price)` para respetar el tick size.

2. **Modelo de Posiciones:**
   - En `ChartStrategy`, el volumen y sentido neto de la posicion abierta se derivan rigurosamente de los fills de ejecucion propios (`MyTrades`), asegurando consistencia frente a desincronizaciones de cuenta.
   - Si se abren posiciones manualmente fuera de esta estrategia, `atas_position` reportara exclusivamente la posicion originada y gestionada por la estrategia.

3. **Tolerancia a Puertos Ocupados:**
   - Si el puerto 8787 se encuentra en uso por otra instancia o proceso, el addon intenta secuencialmente los puertos del 8787 al 8807.
   - El puerto asignado activamente se persiste en `%APPDATA%\ATAS\McpBridge.port` y `%APPDATA%\ATAS X\McpBridge.port`. El cliente Python lee automaticamente este archivo en cada inicializacion.

---

## Resolucion de Problemas (Troubleshooting)

| Sintoma | Causa Probable | Solucion |
|---|---|---|
| `dotnet no se reconoce como comando interno o externo` | El SDK de .NET 8 no esta instalado o la variable PATH no se ha actualizado. | Ejecuta `install_sdk.bat` o `winget install Microsoft.DotNet.SDK.8` y abre una terminal nueva. |
| `http://127.0.0.1:8787/api/health` no responde | La estrategia no esta agregada al grafico o `IsActivated` no esta marcado. | Abre ATAS, agrega `MCP Bridge` desde la ventana de indicadores/estrategias y activa la casilla `IsActivated`. Revisa el archivo `%APPDATA%\ATAS\McpBridge.log`. |
| Error de permisos de red (Access is denied) | Windows HTTP.sys requiere reserva explicita de URL. | Ejecuta en una consola de Administrador: `netsh http add urlacl url=http://localhost:8787/ user=Todos` (o `user=everyone` segun el idioma de Windows). |
| La IA informa que no encuentra las herramientas `atas_*` | El cliente MCP no inicio `server.py` o la ruta en el archivo JSON es incorrecta. | Comprueba que la ruta absoluta en `mcp.json` o `claude_desktop_config.json` coincida con la ubicacion real del archivo `server.py`. |
| Las ordenes no se registran en el broker | La cuenta seleccionada no esta conectada o el portfolio es invalido. | Comprueba en ATAS que la conexion con el broker o data feed este en verde y que hayas seleccionado un portfolio activo en los ajustes de la estrategia. |

---

## Advertencia de Riesgo

El software aqui provisto interactua directamente con plataformas de negociacion bursatil y tiene la capacidad de enviar ordenes ejecutables al mercado. Las operaciones con instrumentos financieros conllevan un riesgo significativo de perdida de capital.

- Pruebe exhaustivamente todas las herramientas e interacciones en un entorno simulado (cuentas DEMO o Market Replay) antes de autorizar cualquier operacion con dinero real.
- Este proyecto se distribuye con fines exclusivamente educativos y tecnicos, sin garantia de rentabilidad ni asesoramiento financiero de ningun tipo.
