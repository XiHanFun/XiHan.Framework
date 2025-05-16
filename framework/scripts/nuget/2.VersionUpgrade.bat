@echo off
rem 设置代码页为简体中文
chcp 936 >nul

title Version Upgrade

echo ========================================
echo  Version Upgrade - XiHan.Framework
echo ========================================
echo.

rem 保存当前目录路径
set "SCRIPT_DIR=%~dp0"

rem 请求管理员权限
if not "%1"=="am_admin" (
    powershell -Command "Start-Process -Verb RunAs -FilePath '%0' -ArgumentList 'am_admin'"
    exit /b
)

rem 切换到脚本所在目录
cd /d "%SCRIPT_DIR%"

rem 检查PowerShell脚本是否存在
if not exist "VersionUpgrade.ps1" (
    echo ERROR: PowerShell script not found: %SCRIPT_DIR%VersionUpgrade.ps1
    echo Please make sure the script exists in the same directory.
    goto :END
)

echo Running version upgrade script...
echo This will help you upgrade project version numbers.
echo Current directory: %CD%
echo.

rem 运行PowerShell脚本升级版本
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Set-Location '%CD%'; & '%SCRIPT_DIR%VersionUpgrade.ps1'"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Version upgrade failed.
    echo.
)

:END
echo.
echo Press any key to exit...
pause >nul 
