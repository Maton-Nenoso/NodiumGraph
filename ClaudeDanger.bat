@echo off
if "%~1"=="" (
    powershell -NoExit -Command "claude --dangerously-skip-permissions"
) else (
    powershell -NoExit -Command "Set-Location '%~1'; claude --dangerously-skip-permissions"
)