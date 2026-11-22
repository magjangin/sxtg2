@echo off
setlocal
chcp 65001 >nul

echo.
echo ==================================================
echo  Roslyn C# AI Outline Generator
echo ==================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "PROJECT_DIR=%SCRIPT_DIR%CsOutlineGenerator"

dotnet run --project "%PROJECT_DIR%" -c Release --no-launch-profile

echo.
pause
