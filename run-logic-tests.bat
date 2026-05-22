@echo off
setlocal

echo ========================================
echo sxtg2 Logic Tests
echo ========================================

set "ROOT=%~dp0"
set "TEST_PROJECT=%ROOT%sxtg2.LogicTests\sxtg2.LogicTests.csproj"

if not exist "%TEST_PROJECT%" (
  echo [ERROR] Test project not found: %TEST_PROJECT%
  exit /b 1
)

echo [INFO] Running logic tests...
dotnet run --project "%TEST_PROJECT%" --configuration Debug
set "EXITCODE=%ERRORLEVEL%"

if "%EXITCODE%"=="0" (
  echo [SUCCESS] Logic tests passed.
) else (
  echo [ERROR] Logic tests failed. ExitCode=%EXITCODE%
)

exit /b %EXITCODE%
