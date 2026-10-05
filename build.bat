@echo off
setlocal enabledelayedexpansion

:: Usage: build.bat [Debug|Release]   (default: Debug)
::   Set the GAME_PATH environment variable to override the game folder; set NO_PAUSE=1 to skip the final pause.
::   build-release.bat is a thin wrapper that calls this script with Release.

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"
if /I not "%CONFIG%"=="Debug" if /I not "%CONFIG%"=="Release" (
    echo [ERROR] Unknown configuration: %CONFIG%  ^(Debug or Release^)
    call :pause_if_interactive
    exit /b 1
)

echo.
echo ========================================
echo.
echo sxtg2 Modding Build Script ^(%CONFIG%^)
echo.
echo ========================================
echo.

:: Project settings
set "PROJECT_NAME=sxtg2"
set "PROJECT_DIR=sxtg2-mod"
set "SOLUTION_FILE=sxtg2.sln"
if not defined GAME_PATH set "GAME_PATH=H:\Sixtar Gate STARTRAIL custom mode"

:: The repository root is derived from the script location (includes the trailing backslash).
set "SOURCE_ROOT=%~dp0"

:: Build paths
set "DLL_NAME=%PROJECT_NAME%.dll"
set "MODS_DIR=%GAME_PATH%\Mods"
set "SOURCE_DLL=%SOURCE_ROOT%%PROJECT_DIR%\bin\%CONFIG%\%DLL_NAME%"
set "TARGET_DLL=%MODS_DIR%\%DLL_NAME%"

:: Find MSBuild
set "MSBUILD_PATH="
for %%v in (18 2026 2022) do (
    for %%e in (Community Professional Enterprise) do (
        if exist "C:\Program Files\Microsoft Visual Studio\%%v\%%e\MSBuild\Current\Bin\MSBuild.exe" (
            set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\%%v\%%e\MSBuild\Current\Bin\MSBuild.exe"
            goto :found
        )
    )
)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
)

:found
if "!MSBUILD_PATH!"=="" (
    echo [ERROR] MSBuild not found.
    echo [ERROR] Please check if Visual Studio is installed.
    call :pause_if_interactive
    exit /b 1
)

echo [INFO] MSBuild path: !MSBUILD_PATH!
echo.

set "SOLUTION_PATH=%SOURCE_ROOT%%SOLUTION_FILE%"

echo [INFO] Starting %CONFIG% build...
echo [INFO] GamePath: !GAME_PATH!
echo.

:: Platform=x64: the Debug|x64 / Release|x64 blocks in the csproj (DEBUG constant, Release optimization) only apply when building x64.
:: UseSharedCompilation=false, so there is no need to kill the compiler server (VBCSCompiler) separately.

:: Restore NuGet packages
echo [INFO] Restoring NuGet packages...
"!MSBUILD_PATH!" "!SOLUTION_PATH!" /p:Configuration=%CONFIG% /p:Platform=x64 /p:GamePath="!GAME_PATH!" /p:UseSharedCompilation=false /nr:false /t:Restore /v:minimal /nologo

:: Build project
echo [INFO] Building project...
"!MSBUILD_PATH!" "!SOLUTION_PATH!" /p:Configuration=%CONFIG% /p:Platform=x64 /p:GamePath="!GAME_PATH!" /p:UseSharedCompilation=false /nr:false /t:Build /v:minimal /nologo

if errorlevel 1 (
    echo.
    echo ========================================
    echo [ERROR] Build failed
    echo ========================================
    call :pause_if_interactive
    exit /b 1
)

echo.
echo ========================================
echo [SUCCESS] Build completed
echo ========================================
echo.

:: Verify DLL file
if not exist "!SOURCE_DLL!" (
    echo [ERROR] DLL file not found: !SOURCE_DLL!
    call :pause_if_interactive
    exit /b 1
)

for %%F in ("!SOURCE_DLL!") do (
    set "FILE_SIZE=%%~zF"
    set "FILE_TIME=%%~tF"
)

echo [INFO] Built DLL file: !SOURCE_DLL!
echo [INFO] File size: !FILE_SIZE! bytes
echo [INFO] Modified time: !FILE_TIME!
echo.

if !FILE_SIZE! LSS 1024 (
    echo [ERROR] DLL file size is too small: !FILE_SIZE! bytes
    call :pause_if_interactive
    exit /b 1
)

:: Copy to Mods directory
echo ========================================
echo [STEP] Copying DLL to Mods directory...
echo ========================================
echo.

if not exist "!GAME_PATH!" (
    echo [ERROR] Game directory not found: !GAME_PATH!
    call :pause_if_interactive
    exit /b 1
)

if not exist "!MODS_DIR!" (
    echo [INFO] Creating Mods directory...
    mkdir "!MODS_DIR!"
)

echo [INFO] Copying !SOURCE_DLL!
echo [INFO]      to !TARGET_DLL!
echo.

copy /Y "!SOURCE_DLL!" "!TARGET_DLL!" >nul

if errorlevel 1 (
    echo [ERROR] File copy failed
    call :pause_if_interactive
    exit /b 1
)

:: Verify copied file
for %%F in ("!TARGET_DLL!") do set "COPIED_SIZE=%%~zF"

if not "!FILE_SIZE!"=="!COPIED_SIZE!" (
    echo [ERROR] File sizes do not match!
    echo [ERROR] Source: !FILE_SIZE! bytes
    echo [ERROR] Copied: !COPIED_SIZE! bytes
    call :pause_if_interactive
    exit /b 1
)

echo ========================================
echo [SUCCESS] DLL copied successfully
echo ========================================
echo.
echo [INFO]  Source: !SOURCE_DLL!
echo [INFO]  Target: !TARGET_DLL!
echo [INFO]  File size: !COPIED_SIZE! bytes
echo.

call :pause_if_interactive
exit /b 0

:pause_if_interactive
if not defined NO_PAUSE pause
exit /b 0
