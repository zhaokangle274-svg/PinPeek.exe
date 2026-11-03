@echo off
setlocal
cd /d "%~dp0"

rem ---- self elevate ----
net session >nul 2>&1
if not %errorlevel%==0 (
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set EXE=%~dp0MabaoClone.exe
if not exist "%EXE%" (
    echo [ERROR] MabaoClone.exe not found next to this script.
    pause
    exit /b 1
)

echo Creating logon task "MabaoClone" (run with highest privileges, no UAC prompt)...
schtasks /create /tn "MabaoClone" /tr "\"%EXE%\"" /sc onlogon /rl highest /f
if %errorlevel%==0 (
    echo.
    echo [OK] Task created. MabaoClone will start automatically at next logon
    echo      with administrator rights and WITHOUT a UAC prompt.
    echo.
    echo      To remove it later, run:  disable_autostart_admin.bat
) else (
    echo.
    echo [FAILED] schtasks returned %errorlevel%
    echo Manual command:
    echo   schtasks /create /tn "MabaoClone" /tr "\"%EXE%\"" /sc onlogon /rl highest /f
)
echo.
pause
