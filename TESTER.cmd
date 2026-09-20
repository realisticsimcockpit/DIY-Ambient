@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Test.ps1"
set "test_result=%ERRORLEVEL%"
pause
exit /b %test_result%
