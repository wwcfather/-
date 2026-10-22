# Halcon贴膜缺陷检测系统

基于 **C#、WPF、MVVM 和 HALCON HDevEngine** 的视觉识别上位机项目。程序在运行时加载 HDevelop 外部过程（`.hdvp`），解析算法接口，通过本地图片序列完成识别，并显示结果图、运行日志及当前会话的历史记录。

本仓库对应桌面 `halconDemo` 项目的源码，保留原有界面和代码结构。

## 核心功能

- **演示登录**：进入主界面前校验固定账号和密码。
- **算法加载**：读取 `.hdvp` 的输入、输出参数定义，自动生成控制参数输入项。
- **参数解析**：支持整数、小数、字符串和逗号分隔的 HALCON 元组。
- **图片序列**：选择本地图片文件夹，按文件名排序，支持上一张、下一张和循环浏览；记忆上次使用的文件夹。
- **算法执行与显示**：调用 HDevEngine 执行过程，在结果图上叠加输出区域和 XLD 轮廓。
- **运行记录**：显示运行日志、OK/NG 结果及本次会话中的识别历史。

## 功能模块

| 模块 | 功能说明 |
| --- | --- |
| 图像来源 | 从本地图片文件夹读取图像，支持按序浏览与循环切换 |
| 算法格式 | 支持加载 `.hdvp` 外部过程，自动解析算法接口与参数 |
| OK/NG | 按算法输出的区域与 XLD 对象数量生成判定结果：总数大于 0 判为 NG，否则判为 OK |
| 通讯配置 | 提供 TCP/串口参数配置界面，支持两种配置面板切换 |
| 记录查询 | 提供独立的记录展示窗口，内置示例记录用于界面演示 |
| 主窗口历史 | 汇总并展示当前会话中的识别记录，便于查看本次运行结果 |
| 参数显示 | 根据算法接口生成控制参数输入项，支持参数编辑与执行配置 |

## 开发与运行环境

- Windows x64。
- **.NET 10 SDK**，项目目标为 `net10.0-windows`，使用 WPF；在 Visual Studio 中开发时需安装相应的 .NET 桌面开发组件。
- HALCON 及其 .NET 组件，包含 `halcondotnet.dll`、`hdevenginedotnet.dll` 和对应的 x64 原生运行库。
- 运行算法所需的有效 HALCON 许可。

工程固定使用 `x64`。HALCON 的托管程序集和原生 DLL 应来自同一安装版本。

### 配置 HALCON 路径

打开 `halconDemo/halconDemo.csproj`，修改“用户配置区”的两项路径：

```xml
<HALCONROOT>D:/你的安装目录/HALCON</HALCONROOT>
<HalconManagedDir>$(HALCONROOT)/bin/dotnet35</HalconManagedDir>
```

- `HALCONROOT` 指向本机 HALCON 安装根目录。
- `HalconManagedDir` 指向同时包含两个 .NET DLL 的目录。不同安装版本的目录名可能不同，以本机实际文件为准。
- 源码中的默认路径为 `D:/HALCON26.05/HALCON-26.05-Progress`，使用前请替换为本机实际安装路径。
- 配置好 `$(HALCONROOT)/bin/x64-win64` 原生库目录后，构建会将其下的 DLL 复制到输出目录。

## 打开与启动

先完成上述 HALCON 路径配置，再选择一种启动方式：

1. 用 Visual Studio 打开 `halconDemo.sln`，将 `halconDemo` 设为启动项目后运行。支持 `.slnx` 的版本也可打开 `halconDemo.slnx`。
2. 在仓库根目录双击 `启动.bat`，脚本会调用 `dotnet build` 编译 Debug，然后启动程序。
3. 在仓库根目录执行：

```powershell
dotnet build .\halconDemo\halconDemo.csproj -c Debug
.\halconDemo\bin\Debug\net10.0-windows\halconDemo.exe
```

演示登录账号为 `admin`，密码为 `admin123`。程序采用本地固定账号校验，便于启动演示和调试。

`检查环境.bat` 可生成 `诊断结果.txt`，同时会尝试还原和编译项目。脚本会检查常见 HALCON 安装目录；使用自定义安装位置时，可结合工程配置核对路径。

## 识别操作流程

1. 在 HDevelop 中准备可调用的外部过程，并保存为 `.hdvp`。将图像声明为输入图形参数，将需要展示的区域或 XLD 声明为输出图形参数。
2. 登录软件，点击“加载算法文件”，选择 `.hdvp`，填写控制参数。
3. 在“图片序列”中点击“浏览…”选择图片文件夹，或输入文件夹路径后按回车。
4. 图片加载且输入条件齐备时会自动执行识别。通过“上一张”“下一张”切换图片。
5. 点击“执行识别”可识别当前图片；对于已识别的图片，再次点击会切换并识别下一张。
6. 查看结果图、日志和主窗口历史记录。正常离屏渲染路径下，区域和 XLD 轮廓均以红色叠加。

界面围绕单图像输入流程设计，通过图片序列为主图像参数提供输入。

在 HDevelop 中将需要展示的区域或轮廓声明为输出图形参数，软件即可通过过程接口获取这些对象，并叠加到结果图中。

控制参数示例：

| 输入文本 | 含义 |
| --- | --- |
| `128` | 整数 |
| `1.5` | 小数 |
| `'rectangle'` | 字符串 |
| `10,20,30` 或 `[10,20,30]` | 数值元组 |
| `'a','b','c'` | 字符串元组 |

图片支持 BMP、PNG、JPG/JPEG、TIF/TIFF、GIF、PCX、PPM、PGM。图片目录设置保存在 `%APPDATA%\halconDemo\settings.txt`。

## 项目结构

```text
.
├─ README.md
├─ .gitignore
├─ halconDemo.sln / halconDemo.slnx
├─ 启动.bat
├─ 检查环境.bat
└─ halconDemo/
   ├─ halconDemo.csproj
   ├─ App.xaml / App.xaml.cs
   ├─ LoginWindow.xaml / LoginWindow.xaml.cs
   ├─ MainWindow.xaml / MainWindow.xaml.cs
   ├─ CommunicationWindow.xaml / CommunicationWindow.xaml.cs
   ├─ QueryWindow.xaml / QueryWindow.xaml.cs
   ├─ Models/                 # 参数描述与算法接口定义
   ├─ ViewModels/             # 加载、参数、图片序列、执行与历史记录
   ├─ Services/               # HDevEngine、图像显示、元组解析
   ├─ match_connector_Exportable.hdev
   └─ SaveLoadShapeModel.hdev
```

两个 `.hdev` 文件提供算法过程改造及模型保存、读取的参考示例。使用时在 HDevelop 中整理并导出 `.hdvp` 外部过程，再准备对应的测试图片与模板模型，即可接入软件的识别流程。

## 编译验证

2026-10-22 在上传副本中完成 Debug 编译验证：**0 个错误**。验证环境为 .NET SDK 10.0.402、HALCON 24.11.1（x64）；构建时通过命令行指定本机 HALCON 路径。

```powershell
dotnet build .\halconDemo\halconDemo.csproj -c Debug -p:HALCONROOT="D:/kingOR/Halcon/HALCON-24.11-Progress-Steady" -p:HalconManagedDir="D:/kingOR/Halcon/HALCON-24.11-Progress-Steady/bin/dotnet35"
```
