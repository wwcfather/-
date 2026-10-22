@echo off
chcp 65001 >nul
title HALCON 项目诊断
cd /d "%~dp0"
set "OUT=%~dp0诊断结果.txt"
echo ===== HALCON 项目诊断 %date% %time% ===== > "%OUT%"
echo. >> "%OUT%"
echo 【说明】本脚本用于找出真实报错，把本文件全文发给 AI 助手即可。 >> "%OUT%"
echo. >> "%OUT%"
echo [0] Windows 版本: >> "%OUT%"
ver >> "%OUT%" 2>&1
echo. >> "%OUT%"
echo [1] dotnet 命令是否存在: >> "%OUT%"
where dotnet >> "%OUT%" 2>&1
if errorlevel 1 echo [严重] 未找到 dotnet 命令 —— .NET SDK 未安装或不在 PATH >> "%OUT%"
echo. >> "%OUT%"
echo [2] 已安装的 .NET SDK (本项目需要 10.x): >> "%OUT%"
dotnet --list-sdks >> "%OUT%" 2>&1
echo. >> "%OUT%"
echo [3] 已安装的 .NET Runtime: >> "%OUT%"
dotnet --list-runtimes >> "%OUT%" 2>&1
echo. >> "%OUT%"
echo [4] Visual Studio 版本: >> "%OUT%"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
  "%VSWHERE%" -products * -requires Microsoft.Component.MSBuild -property displayName >> "%OUT%" 2>&1
  "%VSWHERE%" -products * -requires Microsoft.Component.MSBuild -property installationVersion >> "%OUT%" 2>&1
) else (
  echo 未找到 vswhere >> "%OUT%"
)
echo. >> "%OUT%"
echo [5] HALCON 安装探测(C/D 盘常见路径): >> "%OUT%"
if exist "%HALCONROOT%" echo 环境变量 HALCONROOT=%HALCONROOT% >> "%OUT%"
set "MVFOUND="
for /d %%D in ("C:\Program Files\MVTec\HALCON-*" "C:\Program Files (x86)\MVTec\HALCON-*" "D:\Program Files\MVTec\HALCON-*" "D:\Program Files (x86)\MVTec\HALCON-*") do (
    echo 找到 HALCON 目录: %%D >> "%OUT%"
    echo   该目录下的 halcondotnet.dll(递归列出): >> "%OUT%"
    dir /s /b "%%D\halcondotnet.dll" >> "%OUT%" 2>&1
    echo   该目录下的 hdevenginedotnet.dll(递归列出): >> "%OUT%"
    dir /s /b "%%D\hdevenginedotnet.dll" >> "%OUT%" 2>&1
    set "MVFOUND=1"
)
if not defined MVFOUND (
    echo [严重] 未在 C/D 盘 Program Files 下发现任何 MVTec HALCON 目录 >> "%OUT%"
    echo        若确定已安装到其它盘/路径, 请设置环境变量 HALCONROOT 后重新运行 >> "%OUT%"
    echo        也可能在安装 HALCON 时没有勾选 .NET 开发组件 >> "%OUT%"
)
echo. >> "%OUT%"
echo [6] 还原(restore)结果: >> "%OUT%"
dotnet restore halconDemo\halconDemo.csproj >> "%OUT%" 2>&1
echo. >> "%OUT%"
echo [7] 命令行真实编译结果 (VS 弹窗吞掉的报错会显示在这里): >> "%OUT%"
dotnet build halconDemo\halconDemo.csproj -c Debug >> "%OUT%" 2>&1
echo. >> "%OUT%"
echo 诊断完成，正在打开结果文件... >> "%OUT%"
start notepad "%OUT%"
exit /b 0
