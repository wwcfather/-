@echo off
chcp 65001 >nul
title HALCON 视觉识别平台 - 启动器
cd /d "%~dp0"
echo ==============================================
echo    HALCON 视觉识别平台 - 一键编译并启动
echo ==============================================
echo.
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [错误] 未找到 dotnet 命令。请先安装 .NET SDK —— 用 VS 安装器勾选“.NET 桌面开发”即可。
    pause
    exit /b 1
)
echo [1/2] 正在编译，首次编译约需 1-2 分钟...
dotnet build halconDemo/halconDemo.csproj -c Debug -nologo
if errorlevel 1 (
    echo.
    echo [错误] 编译失败，最常见原因：
    echo   1. 未安装 HALCON，或 halconDemo/halconDemo.csproj 中 HALCONROOT 路径不正确；
    echo   2. 找不到 halcondotnet.dll / hdevenginedotnet.dll；
    echo   3. .NET SDK 版本不符。
    echo 请把上方完整报错信息发给 AI 助手定位。
    pause
    exit /b 1
)
set "EXE=halconDemo/bin/Debug/net10.0-windows/halconDemo.exe"
if not exist "%EXE%" (
    echo [错误] 未找到编译产物：%EXE%
    pause
    exit /b 1
)
echo [2/2] 启动程序...
start "" "%EXE%"
echo 程序已启动，本窗口可关闭。
timeout /t 3 >nul
