@echo off
rem ============================================================
rem  ATAS MCP Server - Server-Sent Events (SSE) mode
rem  Listens on http://127.0.0.1:8000/sse for web applications,
rem  remote IDEs, cloud agents, OpenClaw, and browser tools.
rem ============================================================
cd /d "%~dp0"

python -c "import mcp" >nul 2>&1
if errorlevel 1 (
    echo Installing dependencies...
    python -m pip install -r requirements.txt
)

echo Starting ATAS MCP Server in SSE mode on http://127.0.0.1:8000/sse...
python server.py --transport sse --host 127.0.0.1 --port 8000
