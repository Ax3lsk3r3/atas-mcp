@echo off
rem ============================================================
rem  ATAS MCP server - run this after ATAS is running with the
rem  "MCP Bridge" strategy active on a chart.
rem  Speaks MCP over stdio: Claude Desktop, Cursor, Claude Code...
rem ============================================================
cd /d "%~dp0"

python -c "import mcp" >nul 2>&1
if errorlevel 1 (
    echo Installing dependencies...
    python -m pip install -r requirements.txt
)

echo Starting ATAS MCP server (stdio)...
python server.py
