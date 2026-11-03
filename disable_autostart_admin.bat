@echo off
setlocal

net session >nul 2>&1
if not %errorlevel%==0 (
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo Removing task "MabaoClone"...
schtasks /delete /tn "MabaoClone" /f
if %errorlevel%==0 (
    echo [OK] Task removed. MabaoClone will no longer auto-start.
) else (
    echo [INFO] Nothing removed (task may not exist).
)
echo.
pause
