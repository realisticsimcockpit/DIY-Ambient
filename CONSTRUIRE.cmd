@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build.ps1" %*
set "build_result=%ERRORLEVEL%"
echo.
if not "%build_result%"=="0" echo ECHEC : conserver le message ci-dessus. Aucune DLL ne doit etre installee.
pause
exit /b %build_result%
