# Configuracion del servidor MCP de ATAS

El servidor MCP (`server.py`) se comunica a traves de stdio (estandar de Model Context Protocol). Cualquier cliente compatible puede conectarse ejecutando `python server.py`.

Ruta del proyecto:
`C:\Users\ax3lsk3r3\Desktop\atas-mcp`

---

## 1. Cursor

Ubicacion del archivo:
`%USERPROFILE%\.cursor\mcp.json`

Contenido recomendado:

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

O desde la interfaz grafica:
1. Abre Settings en Cursor (Ctrl + ,).
2. Busca la seccion MCP.
3. Haz clic en "Add new MCP server".
4. Type: stdio.
5. Command: python.
6. Arguments: C:\Users\ax3lsk3r3\Desktop\atas-mcp\server.py.

---

## 2. Claude Desktop

Ubicacion del archivo:
`%APPDATA%\Claude\claude_desktop_config.json`

Contenido recomendado:

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

Despues de guardar el archivo, reinicia Claude Desktop. En el selector de herramientas apareceran las 12 herramientas `atas_*`.

---

## 3. Claude Code (CLI)

Ejecuta en tu terminal:

```bash
claude mcp add atas -- python "C:\Users\ax3lsk3r3\Desktop\atas-mcp\server.py"
```

---

## 4. Antigravity CLI / IDE

Ubicacion del archivo:
`%USERPROFILE%\.gemini\config\mcp_config.json`

Contenido:

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

---

## 5. Windsurf / Cline / Roo Code (VS Code)

Para extensiones de VS Code basadas en MCP, anade al bloque `mcpServers`:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "C:/Users/ax3lsk3r3/Desktop/atas-mcp/server.py"
      ],
      "disabled": false,
      "autoApprove": []
    }
  }
}
```

---

## 6. Variables de entorno opcionales

Si deseas personalizar el puerto o host de comunicacion con el bridge de ATAS:

- `ATAS_MCP_PORT`: Puerto HTTP donde escucha el addon (por defecto 8787 o el detectado en `%APPDATA%\ATAS\McpBridge.port`).
- `ATAS_MCP_HOST`: Host donde se conecta el cliente (por defecto `127.0.0.1`).

Ejemplo en configuracion MCP:

```json
{
  "mcpServers": {
    "atas": {
      "command": "python",
      "args": [
        "C:/Users/ax3lsk3r3/Desktop/atas-mcp/server.py"
      ],
      "env": {
        "ATAS_MCP_PORT": "8787",
        "ATAS_MCP_HOST": "127.0.0.1"
      }
    }
  }
}
```

---

## 7. Aviso de seguridad

- El bridge HTTP de ATAS unicamente escucha en interfaces locales (`127.0.0.1` / `localhost`) y no expone puertos a redes externas.
- Las herramientas de envio y gestion de ordenes (`atas_place_order`, `atas_cancel_order`, `atas_close_position`) ejecutan transacciones directas en la cuenta o portfolio seleccionado en ATAS.
- Se recomienda operar inicialmente en cuentas de simulacion (DEMO o Replay) antes de utilizar cuentas reales con capital en riesgo.
