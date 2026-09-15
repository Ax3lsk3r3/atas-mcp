@echo off
rem ============================================================
rem  Builds the ATAS MCP Bridge addon and deploys it to ATAS.
rem  Requires the .NET 8 SDK (install: winget install Microsoft.DotNet.SDK.8).
rem ============================================================
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet not found. Install the .NET 8 SDK first:
    echo         winget install Microsoft.DotNet.SDK.8
    exit /b 1
)

echo Building ATAS.McpBridge...
dotnet build ATAS.McpBridge\ATAS.McpBridge.csproj -c Release
if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

set "STRATEGIES_DIR=%APPDATA%\ATAS\Strategies"
if not exist "%STRATEGIES_DIR%" mkdir "%STRATEGIES_DIR%"

copy /y "out\ATAS.McpBridge.dll" "%STRATEGIES_DIR%\" >nul
if errorlevel 1 (
    echo [ERROR] Could not copy to %STRATEGIES_DIR%
    exit /b 1
)
echo [OK] ATAS.McpBridge.dll deployed to %STRATEGIES_DIR%

set "STRATEGIES_DIR_X=%APPDATA%\ATAS X\Strategies"
if exist "%APPDATA%\ATAS X" (
    if not exist "%STRATEGIES_DIR_X%" mkdir "%STRATEGIES_DIR_X%"
    copy /y "out\ATAS.McpBridge.dll" "%STRATEGIES_DIR_X%\" >nul
    if not errorlevel 1 (
        echo [OK] ATAS.McpBridge.dll deployed to %STRATEGIES_DIR_X%
    )
)

echo.
echo [OK] Deployment complete.
echo Next steps:
echo 1. Open ATAS (or ATAS X) and open any chart (e.g. NQ, ES, BTC).
echo 2. Right-click on chart -^> Indicators -^> MCP Bridge -^> Add.
echo 3. Check "IsActivated" and select your portfolio / account.
echo 4. Start the MCP server using start_mcp.bat or connect via Cursor / Claude.
