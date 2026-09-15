@echo off
rem ============================================================
rem  Installs the .NET 8 SDK (needed to compile the ATAS addon)
rem  Run this in its own terminal window if .NET 8 SDK is missing.
rem ============================================================
echo.
echo Instalando .NET 8 SDK... (puede tardar unos minutos)
echo.
winget install Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements --silent --disable-interactivity
echo.
echo ==== RESULTADO ====
dotnet --list-sdks
echo.
echo Si ves una linea con 8.x arriba, la instalacion fue correcta.
echo Cierra esta ventana y procede a ejecutar build.bat.
echo.
pause
