@echo off
rem 1. Ensure Fedora-Desktop is awake (starts in ~0.5s if already running, ~2s if cold)
wsl.exe -d Fedora-Desktop -u developer -- true

rem 2. Launch Windows Remote Desktop using pre-configured profile (or localhost:3390)
if exist "%~dp0Fedora-Desktop.rdp" (
    start mstsc.exe "%~dp0Fedora-Desktop.rdp"
) else (
    start mstsc.exe /v:127.0.0.1:3390
)
