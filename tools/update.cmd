@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0update.ps1" -Show
exit /b %errorlevel%
