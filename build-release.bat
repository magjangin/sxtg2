@echo off
:: Release build: calls build.bat with the Release configuration (the two scripts used to be 156-line duplicates).
call "%~dp0build.bat" Release
exit /b %errorlevel%
