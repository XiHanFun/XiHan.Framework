@echo off
title UnlistNugetPackages

set "SCRIPT_DIR=%~dp0"

if not "%1"=="am_admin" (
    powershell -Command "Start-Process -Verb RunAs -FilePath '%0' -ArgumentList 'am_admin'"
    exit /b
)

cd /d "%SCRIPT_DIR%"

if not exist "UnlistNugetPackages.ps1" (
    echo [ERROR] UnlistNugetPackages.ps1 not found: %SCRIPT_DIR%
    goto :END
)

echo ========================================
echo  UnlistNugetPackages - XiHan.Framework
echo ========================================

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Set-Location '%CD%'; & '%SCRIPT_DIR%UnlistNugetPackages.ps1'"

:END
echo.
pause
