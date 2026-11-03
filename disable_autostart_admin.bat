@echo off
setlocal

net session >nul 2>&1
if not %errorlevel%==0 (
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo Removing task "PinPeek"...
schtasks /delete /tn "PinPeek" /f
if %errorlevel%==0 (
    echo [OK] Task removed. PinPeek will no longer auto-start.
) else (
    echo [INFO] Nothing removed (task may not exist).
)
echo.
pause
