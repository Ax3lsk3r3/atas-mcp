# ATAS MCP Bridge

[![CI](https://github.com/Ax3lsk3r3/atas-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/Ax3lsk3r3/atas-mcp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Python: 3.10+](https://img.shields.io/badge/python-3.10%2B-blue.svg)](https://www.python.org/downloads/)
[![MCP: 2.0](https://img.shields.io/badge/MCP-2.0-orange.svg)](https://modelcontextprotocol.io/)
[![Platform: ATAS 7.x & 8.x](https://img.shields.io/badge/ATAS-7.x%20%7C%208.x-green.svg)](https://atas.net/)
[![Release: v1.0.0](https://img.shields.io/badge/release-v1.0.0-brightgreen.svg)](https://github.com/Ax3lsk3r3/atas-mcp/releases)

Servidor MCP (Model Context Protocol) para conectar **ATAS Platform** (7.x) y **ATAS X** (8.x) con agentes de Inteligencia Artificial y asistentes de desarrollo.

[Read in English](README.md)

---

## Descripcion General

ATAS MCP Bridge une el order flow institucional de ATAS con modelos modernos de Inteligencia Artificial. Expone cotizaciones en vivo, libro de ordenes en profundidad (DOM), velas con delta de volumen, posiciones de la cuenta, ordenes abiertas, ejecuciones y enrutamiento de ordenes directamente a asistentes de IA.

Compatible con los principales agentes de programacion e interfaces MCP:
- **OpenCode**
- **Qwen Code**
- **Kiro (CLI e IDE)**
- **Claude Code**
- **OpenAI Codex / Developers Platform**
- **OpenClaw**
- **Kimi Kode**
- **Z Code**
- **Google Antigravity (AGY CLI e IDE)**
- **Cursor**
- **Claude Desktop**
- **Windsurf / VS Code (Cline y Roo Code)**

---

## Arquitectura

```
+------------------------------------+          HTTP / SSE (127.0.0.1:8787)         +----------------------+
|           ATAS Platform            | <------------------------------------------> |   MCP Server (Py)    |
|            "MCP Bridge"            |             GET  /api/*                      |      server.py       |
|    (ChartStrategy en C# / .NET)    |             POST /api/order                  |      (mcp 2.0)       |
+------------------------------------+             GET  /api/stream                 +----------------------+
                                                                                               ^
                                                                                               | MCP (stdio)
                                                                                               v
                                                                                    +----------------------+
                                                                                    |    Clientes de IA    |
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

### Flujo de Comunicacion:
1. **Addon C# (`ATAS.McpBridge`):** Se ejecuta dentro de ATAS como una `ChartStrategy` nativa. Inicia un servidor local HTTP y Server-Sent Events (SSE) en `127.0.0.1:8787`.
2. **Seguridad de Hilos y Diseno Lock-Free:** Todas las operaciones con la API de ATAS se realizan estrictamente en el hilo principal dentro de `OnCalculate()`. Las peticiones de ejecucion se encolan en una `ConcurrentQueue` segura y se procesan en el siguiente tick. Las lecturas se responden de inmediato mediante snapshots JSON en memoria volatil sin bloquear el calculo de mercado.
3. **Servidor MCP Python (`server.py`):** Se comunica via `stdio` implementando la especificacion de Model Context Protocol y exponiendo 12 herramientas robustas con control de excepciones estructurado.
4. **Cliente HTTP Python (`atas_client.py`):** Sin dependencias de terceros (solo libreria estandar de Python), con deteccion automatica de puerto y soporte para variables de entorno.

---

## Herramientas MCP Disponibles (12 Tools)

| Herramienta | Descripcion | Parametros |
|---|---|---|
| `atas_status` | Estado de conexion del bridge, simbolo, portfolio, tick size, estado activado y puerto. | Ninguno |
| `atas_quote` | Cotizacion en vivo: ultimo precio, mejor bid/ask con volumenes, desbalance del DOM, spread y tick size. | Ninguno |
| `atas_dom` | Libro de ordenes en profundidad (DOM): bids ordenados descendentes y asks ascendentes con precio y volumen. | `levels` (int, por defecto: 15) |
| `atas_candles` | Velas historicas (hasta 2000 barras): apertura, maximo, minimo, cierre, volumen, delta, timestamp y vela actual en formacion. | Ninguno |
| `atas_position` | Detalles de la posicion abierta: volumen neto, precio medio de entrada, direccion (Buy/Sell), PnL abierto y PnL cerrado. | Ninguno |
| `atas_orders` | Ordenes de trabajo activas: ID, direccion, tipo de orden, precio, precio de disparo, volumen ejecutado y pendiente. | Ninguno |
| `atas_trades` | Registro de ejecuciones (fills) concluidas en la sesion activa. | Ninguno |
| `atas_snapshot` | Consolida status, quote, DOM, candles, orders, position y trades en una sola llamada. | Ninguno |
| `atas_place_order` | Envia una orden a mercado o limite. El precio se ajusta automaticamente al tick size del activo. | `direction` ("buy"/"sell"), `order_type` ("limit"/"stop"/"market"/"stoplimit"), `qty` (float), `price` (float opcional), `trigger_price` (float opcional), `comment` (string opcional) |
| `atas_cancel_order` | Cancela una orden de trabajo por su ID unico. | `order_id` (string) |
| `atas_close_position` | Cierra (flatten) la posicion actual mediante una orden a mercado en sentido opuesto. | `volume` (float opcional), `direction` (string opcional) |
| `atas_bridge_log` | Obtiene las ultimas lineas de diagnostico del addon dentro de ATAS para depuracion. | Ninguno |

---

## Endpoints HTTP y SSE del Addon

El addon expone en `http://127.0.0.1:8787` los siguientes endpoints:

| Endpoint | Metodo | Descripcion |
|---|---|---|
| `/api/health` | GET | Verificacion de estado (`{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}`). |
| `/api/status` | GET | Metadatos de la plataforma, instrumento, codigo de seguridad, portfolio y tick size. |
| `/api/quote` | GET | Cotizacion actual, mejor bid/ask y desbalance acumulado del libro. |
| `/api/dom` | GET | Instantanea del libro de ordenes en profundidad. |
| `/api/candles` | GET | Arreglo de velas cerradas mas barra en curso. |
| `/api/orders` | GET | Lista de ordenes activas en el sistema. |
| `/api/position` | GET | Resumen de la posicion neta calculada a partir de los fills de la estrategia. |
| `/api/trades` | GET | Registro de transacciones de la sesion. |
| `/api/snapshot` | GET | Conjunto consolidado de metricas de mercado y cuenta. |
| `/api/log` | GET | Lineas de registro del buffer interno de diagnostico. |
| `/api/order` | POST | Envio de nueva orden (`{"direction":"buy","type":"limit","qty":1,"price":22000.25}`). |
| `/api/order/cancel` | POST | Cancelacion de orden existente (`{"id":"<order_id>"}`). |
| `/api/position/close` | POST | Cierre inmediato de la posicion abierta (`{}`). |
| `/api/stream` | GET (SSE) | Server-Sent Events en tiempo real: `quote` (200ms throttle), `dom` (500ms throttle), `position` (1000ms throttle), `orderSent`, `orderFailed`, `orderCancelSent`, `flattenSent`. |

---

## Requisitos Previos

- **Sistema Operativo:** Windows 10 / 11 (x64)
- **Plataforma:** ATAS Platform (7.x) o ATAS X (8.x) instalada
- **SDK de .NET:** .NET 8 SDK o superior (`dotnet --version` o `winget install Microsoft.DotNet.SDK.8`)
- **Python:** Python 3.10 o superior con pip

---

## Instalacion y Puesta en Marcha

### 1. Clonar el repositorio e instalar dependencias

```bash
git clone https://github.com/Ax3lsk3r3/atas-mcp.git
cd atas-mcp
pip install -r requirements.txt
```

Opcionalmente, instalalo en modo editable para tener disponible el comando `atas-mcp`:

```bash
pip install -e .
```

### 2. Compilar y desplegar el Addon C#

Ejecuta el script de compilacion:

```bash
build.bat
```

Este script compila el proyecto en modo Release y copia `ATAS.McpBridge.dll` en los directorios correspondientes:
- `%APPDATA%\ATAS\Strategies\` (ATAS Platform 7.x)
- `%APPDATA%\ATAS X\Strategies\` (ATAS X 8.x)

### 3. Vincular la Estrategia en ATAS

1. Inicia ATAS (o ATAS X).
2. Abre un grafico con el instrumento que desees operar (ej. NQ, ES, BTCUSDT, EURUSD).
3. Haz clic derecho sobre el grafico -> selecciona **Indicators** (o Agregar Indicador).
4. Busca **MCP Bridge** y anadelo al grafico.
5. En el panel de configuracion lateral de la estrategia:
   - Marca la casilla **`IsActivated`**.
   - Selecciona tu cuenta o portfolio (se recomienda una cuenta demo o de simulacion).
6. Comprueba en tu navegador web:
   - Abre `http://127.0.0.1:8787/api/health`
   - Respuesta esperada: `{"ok":true,"id":"atas-mcp-bridge","version":"1.0.0"}`

### 4. Ejecutar el Smoke Test Automatizado

Verifica el cliente de Python sin necesidad de abrir ATAS:

```bash
python test_client.py
```

Las 13 pruebas deben finalizar en `ALL TESTS PASSED`.

### 5. Iniciar el Servidor MCP

```bash
start_mcp.bat
```

O directamente:

```bash
python server.py
```

---

## Ejemplos de Configuracion para Clientes de IA

En [mcp-config-examples.md](mcp-config-examples.md) encontraras guias detalladas para cada cliente.

Sustituye `<PATH_TO_ATAS_MCP>` por la ruta absoluta a tu clon local (ejemplo: `C:/trading/atas-mcp` o `C:\\trading\\atas-mcp`).

### OpenCode
En `~/.config/opencode/config.json` o `opencode.json`:
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
Por consola:
```bash
qwen mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### Kiro (CLI e IDE)
Por consola:
```bash
kiro mcp add atas python "<PATH_TO_ATAS_MCP>/server.py"
```

### Claude Code (CLI)
Por consola:
```bash
claude mcp add atas -- python "<PATH_TO_ATAS_MCP>/server.py"
```

### Google Antigravity (AGY)
En `%USERPROFILE%\.gemini\config\mcp_config.json`:
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
En `%USERPROFILE%\.cursor\mcp.json`:
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
En `%APPDATA%\Claude\claude_desktop_config.json`:
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

## Resolucion de Problemas

| Problema | Causa Probable | Solucion |
|---|---|---|
| `dotnet no se reconoce` | El SDK de .NET 8 no esta instalado o la variable PATH no se ha actualizado. | Ejecuta `install_sdk.bat` o `winget install Microsoft.DotNet.SDK.8` y abre una terminal nueva. |
| `http://127.0.0.1:8787/api/health` no responde | La estrategia no esta agregada al grafico o `IsActivated` no esta marcado. | Abre ATAS, agrega `MCP Bridge` via indicadores del grafico y marca `IsActivated`. Revisa `%APPDATA%\ATAS\McpBridge.log`. |
| Error de permisos de red (Access is denied) | Windows HTTP.sys requiere reserva explicita de URL. | Ejecuta en consola de Administrador: `netsh http add urlacl url=http://localhost:8787/ user=Todos` (o `user=Everyone`). |
| El cliente de IA no ve las herramientas `atas_*` | Ruta incorrecta en el archivo de configuracion JSON. | Comprueba que la ruta en el archivo de configuracion de tu cliente apunte a la ruta absoluta de `server.py`. |
| Las ordenes no se registran en el broker | La cuenta seleccionada no esta conectada o el portfolio es invalido. | Comprueba en ATAS que la conexion con el broker este en verde y que hayas seleccionado un portfolio activo en los ajustes de la estrategia. |

---

## Advertencia de Riesgo

El software aqui provisto interactua directamente con plataformas de negociacion bursatil y tiene la capacidad de enviar ordenes ejecutables al mercado. Las operaciones con instrumentos financieros conllevan un riesgo significativo de perdida de capital.

- Pruebe exhaustivamente todas las herramientas e interacciones en un entorno simulado (cuentas DEMO o Market Replay) antes de autorizar cualquier operacion con dinero real.
- Este proyecto se distribuye con fines exclusivamente educativos y tecnicos, sin garantia de rentabilidad ni asesoramiento financiero de ningun tipo.

---

## Licencia

Licencia MIT. Consulta [LICENSE](LICENSE) para mas detalles.
