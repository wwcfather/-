using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;
using Microsoft.Win32;
using halconDemo.Models;
using halconDemo.Services;

namespace halconDemo.ViewModels;

/// <summary>主窗口视图模型：加载算法、选择图像、执行识别、显示结果。</summary>
public class MainViewModel : ObservableObject
{
    private readonly HDevEngineService _engineService = new();
    private AlgorithmSignature? _signature;

    /// <summary>图片文件夹批量浏览支持的扩展名。</summary>
    private static readonly string[] ImageExtensions =
        { ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".gif", ".pcx", ".ppm", ".pgm" };

    /// <summary>当前图片文件夹中的全部图片 (按文件名排序)。</summary>
    private readonly List<string> _imageFiles = new();

    /// <summary>当前图片在 _imageFiles 中的下标；-1 表示当前图是手动选择的、不在序列中。</summary>
    private int _imageIndex = -1;

    /// <summary>自动切换的目标图像输入参数：手动选图或自动切换时都会更新它。</summary>
    private ParamViewModel? _primaryImageParam;

    /// <summary>最近一次加载的图片文件完整路径，用于在没有设置图片文件夹时自动推断文件夹。</summary>
    private string? _lastPickedFile;

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "halconDemo", "settings.txt");

    public ObservableCollection<ParamViewModel> InputIconicParams { get; } = new();
    public ObservableCollection<ParamViewModel> InputControlParams { get; } = new();
    public ObservableCollection<ParamViewModel> OutputIconicParams { get; } = new();
    public ObservableCollection<ParamViewModel> OutputControlParams { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>本次运行期间的识别历史（临时记录，程序关闭即丢失），最新一条在最前。</summary>
    public ObservableCollection<HistoryRecord> HistoryRecords { get; } = new();

    private string _algorithmName = "未加载";
    public string AlgorithmName
    {
        get => _algorithmName;
        set => SetProperty(ref _algorithmName, value);
    }

    private string _algorithmPath = string.Empty;
    public string AlgorithmPath
    {
        get => _algorithmPath;
        set => SetProperty(ref _algorithmPath, value);
    }

    private BitmapSource? _resultImage;
    public BitmapSource? ResultImage
    {
        get => _resultImage;
        set => SetProperty(ref _resultImage, value);
    }

    private string _judgeText = string.Empty;
    /// <summary>OK/NG 判定文字；未识别时为空。</summary>
    public string JudgeText
    {
        get => _judgeText;
        set => SetProperty(ref _judgeText, value);
    }

    private Brush _judgeBackground = Brushes.Transparent;
    public Brush JudgeBackground
    {
        get => _judgeBackground;
        set => SetProperty(ref _judgeBackground, value);
    }

    public Visibility JudgeVisibility =>
        string.IsNullOrEmpty(JudgeText) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>根据本次输出的区域/轮廓数量设置 OK/NG 判定：无任何缺陷图形 → OK，否则 NG。</summary>
    private void SetJudge(bool isNg)
    {
        JudgeText = isNg ? "NG" : "OK";
        JudgeBackground = isNg
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
            : new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
        OnPropertyChanged(nameof(JudgeVisibility));
    }

    private void ClearJudge()
    {
        JudgeText = string.Empty;
        JudgeBackground = Brushes.Transparent;
        OnPropertyChanged(nameof(JudgeVisibility));
    }

    private string _status = "就绪";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private string _imageFolder = string.Empty;
    /// <summary>图片文件夹路径：可直接编辑后回车，或点「浏览…」选择。</summary>
    public string ImageFolder
    {
        get => _imageFolder;
        set => SetProperty(ref _imageFolder, value);
    }

    private string _currentImageFile = string.Empty;
    /// <summary>当前显示的图片文件名。</summary>
    public string CurrentImageFile
    {
        get => _currentImageFile;
        set => SetProperty(ref _currentImageFile, value);
    }

    private string _imagePosition = "未选择文件夹";
    /// <summary>当前图片位置，如 "3 / 25"。</summary>
    public string ImagePosition
    {
        get => _imagePosition;
        set => SetProperty(ref _imagePosition, value);
    }

    public ICommand LoadAlgorithmCommand { get; }
    public ICommand ExecuteCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand PickIconicCommand { get; }
    public ICommand ClearLogsCommand { get; }
    public ICommand BrowseImageFolderCommand { get; }
    public ICommand ApplyImageFolderCommand { get; }
    public ICommand PrevImageCommand { get; }
    public ICommand NextImageCommand { get; }

    public MainViewModel()
    {
        LoadAlgorithmCommand = new RelayCommand(_ => LoadAlgorithm());
        ExecuteCommand = new RelayCommand(_ => Execute());
        ResetCommand = new RelayCommand(_ => Reset());
        PickIconicCommand = new RelayCommand(PickIconic);
        ClearLogsCommand = new RelayCommand(_ => Logs.Clear());
        BrowseImageFolderCommand = new RelayCommand(_ => BrowseImageFolder());
        ApplyImageFolderCommand = new RelayCommand(_ => ApplyImageFolder(ImageFolder));
        PrevImageCommand = new RelayCommand(_ => StepImage(-1));
        NextImageCommand = new RelayCommand(_ => StepImage(1));

        Log("欢迎使用 HALCON 视觉识别平台。请先「加载算法文件」(.hdvp)。");
        LoadSettings();
    }

    private void Log(string message)
        => Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

    // ---------- 设置持久化：记住上次使用的图片文件夹 ----------

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;

            foreach (string line in File.ReadAllLines(SettingsFile))
            {
                int pos = line.IndexOf('=');
                if (pos <= 0) continue;

                string key = line[..pos].Trim();
                string value = line[(pos + 1)..].Trim();
                if (key == "ImageFolder" && Directory.Exists(value))
                {
                    _imageFolder = value;
                    OnPropertyChanged(nameof(ImageFolder));
                    RefreshImageFiles(log: false);
                }
            }
        }
        catch
        {
            // 设置读取失败不影响主流程
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllLines(SettingsFile, new[] { $"ImageFolder={ImageFolder}" });
        }
        catch
        {
            // 设置保存失败不影响主流程
        }
    }

    // ---------- 加载算法 ----------

    private void LoadAlgorithm()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 HALCON 导出的算法文件",
            Filter = "HALCON 过程文件 (*.hdvp)|*.hdvp|HALCON 程序文件 (*.hdev)|*.hdev|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            ClearParameters();
            string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();

            if (ext == ".hdvp")
            {
                _signature = _engineService.LoadProcedure(dialog.FileName);
                AlgorithmPath = dialog.FileName;
                AlgorithmName = _signature.Name;
                BuildParameters(_signature);
                Log($"已加载算法过程「{_signature.Name}」：输入 {_signature.Inputs.Count} 项，输出 {_signature.Outputs.Count} 项。");
                Status = "算法加载完成";

                if (_signature.Inputs.Count == 0)
                    Log("注意：该过程没有声明任何输入参数。若算法内部用 read_image 读取固定路径图片，软件无法为它切换图片或更改图片路径；请在 HDevelop 中把图像变量加入过程的「输入图形参数」后重新导出 .hdvp。");

                // 算法加载后，如果已设置图片文件夹，自动把当前图片填入图像输入参数。
                if (_imageFiles.Count > 0)
                    ShowImageAt(_imageIndex < 0 ? 0 : _imageIndex, "自动填入当前图片");
            }
            else
            {
                _signature = null;
                Log("当前仅支持 HALCON 过程文件 (.hdvp)。请在 HDevelop 中使用「文件 → 导出 → 程序 (过程)」把算法导出为过程后再加载。");
                Status = "不支持的算法文件类型";
            }
        }
        catch (Exception ex)
        {
            _signature = null;
            Log("加载算法失败：" + ex.Message);
            Status = "加载失败";
        }
    }

    private void BuildParameters(AlgorithmSignature signature)
    {
        foreach (ParamDescriptor p in signature.InputIconics)
            InputIconicParams.Add(new ParamViewModel(p));
        foreach (ParamDescriptor p in signature.InputControls)
            InputControlParams.Add(new ParamViewModel(p));
        foreach (ParamDescriptor p in signature.OutputIconics)
            OutputIconicParams.Add(new ParamViewModel(p));
        foreach (ParamDescriptor p in signature.OutputControls)
            OutputControlParams.Add(new ParamViewModel(p));
    }

    // ---------- 图片文件夹 / 批量浏览 ----------

    private void BrowseImageFolder()
    {
        var dialog = new OpenFolderDialog { Title = "选择图片文件夹" };
        if (Directory.Exists(ImageFolder))
            dialog.InitialDirectory = ImageFolder;

        if (dialog.ShowDialog() != true) return;

        ApplyImageFolder(dialog.FolderName);
    }

    /// <summary>应用图片文件夹：刷新图片列表并加载第一张 (或恢复当前位置)。</summary>
    private void ApplyImageFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            Log("图片文件夹路径为空：请点「浏览…」选择，或在路径框输入后按回车。");
            return;
        }

        string full;
        try { full = Path.GetFullPath(folder); }
        catch (Exception ex)
        {
            Log($"图片文件夹路径无效「{folder}」：{ex.Message}");
            return;
        }

        if (!Directory.Exists(full))
        {
            Log($"图片文件夹不存在：{full}");
            return;
        }

        ImageFolder = full;
        SaveSettings();
        RefreshImageFiles(log: true);

        if (_imageFiles.Count > 0)
            ShowImageAt(0, "加载文件夹中的第一张");
    }

    private void RefreshImageFiles(bool log)
    {
        _imageFiles.Clear();
        try
        {
            if (Directory.Exists(ImageFolder))
            {
                _imageFiles.AddRange(Directory
                    .EnumerateFiles(ImageFolder)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            Log("枚举图片文件失败：" + ex.Message);
        }

        if (_imageFiles.Count == 0)
        {
            _imageIndex = -1;
            ImagePosition = "文件夹中没有图片";
            CurrentImageFile = string.Empty;
            if (log)
                Log($"在「{ImageFolder}」中未找到图片文件（支持 bmp/png/jpg/jpeg/tif/tiff/gif 等格式）。");
        }
        else if (log)
        {
            ImagePosition = $"共 {_imageFiles.Count} 张";
            Log($"已设置图片文件夹：{ImageFolder}（共 {_imageFiles.Count} 张）。");
        }
    }

    /// <summary>加载指定下标的图片并填入图像输入参数；越界时循环切换。</summary>
    private void ShowImageAt(int index, string reason, bool refreshResult = true)
    {
        if (_imageFiles.Count == 0)
        {
            Log("尚未设置图片文件夹：请先在「图片序列」中选择图片所在文件夹。");
            return;
        }

        if (index < 0) index = _imageFiles.Count - 1;          // 循环到末尾
        if (index >= _imageFiles.Count) index = 0;              // 循环到开头

        string file = _imageFiles[index];
        try
        {
            HOperatorSet.ReadImage(out HObject image, file);

            ParamViewModel? target = ResolvePrimaryImageParam();
            if (target == null)
            {
                Log("该过程没有图像输入参数，图片无法传给算法：请在 HDevelop 中编辑过程接口，把图像变量设为「输入图形参数」后重新导出 .hdvp（若过程内部用 read_image 读固定路径，软件无法控制其图片）。");
                return;
            }

            target.IconicObject = image;
            target.FilePath = file;
            target.Thumbnail = HalconImageHelper.ToBitmapSource(image);
            _currentImageRecognized = false;   // 换了新图，识别状态复位
            ClearJudge();

            _imageIndex = index;
            _lastPickedFile = file;
            _primaryImageParam = target;
            CurrentImageFile = Path.GetFileName(file);
            ImagePosition = $"{_imageIndex + 1} / {_imageFiles.Count}";
            Log($"{reason}：{Path.GetFileName(file)}（{ImagePosition}）→ 参数「{target.Name}」");

            if (refreshResult && _signature != null &&
                InputIconicParams.All(p => p.IconicObject != null))
            {
                // 切换图片后同步刷新识别结果，避免右侧仍停留在上一张的结果。
                RecognizeCurrent();
            }
        }
        catch (Exception ex)
        {
            Log($"读取图片失败 {file}：{ex.Message}");
        }
    }

    /// <summary>
    /// 选择自动切换的目标图像输入参数：优先用上一次被选图/自动切换命中的参数，
    /// 其次选"当前已经是图像"的输入。这样多图像输入算法 (如 模板图 + 待检图) 下
    /// 自动切换只替换待检图，不会覆盖模板。
    /// </summary>
    private ParamViewModel? ResolvePrimaryImageParam()
    {
        if (_primaryImageParam != null && InputIconicParams.Contains(_primaryImageParam))
            return _primaryImageParam;

        _primaryImageParam = InputIconicParams.FirstOrDefault(p => HalconImageHelper.IsImage(p.IconicObject))
                             ?? InputIconicParams.FirstOrDefault();
        return _primaryImageParam;
    }

    private void PickIconic(object? parameter)
    {
        if (parameter is not ParamViewModel param) return;

        var dialog = new OpenFileDialog
        {
            Title = $"为参数「{param.Name}」选择图像",
            Filter = "图像文件|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.gif;*.pcx;*.ppm;*.pgm|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            HOperatorSet.ReadImage(out HObject image, dialog.FileName);
            param.IconicObject = image;
            param.FilePath = dialog.FileName;
            param.Thumbnail = HalconImageHelper.ToBitmapSource(image);
            _currentImageRecognized = false;   // 手动换图，识别状态复位
            ClearJudge();
            _lastPickedFile = dialog.FileName;
            _primaryImageParam = param;

            // 手动选择的图片若在当前文件夹序列中，则同步位置，后续"自动下一张"从它的下一张继续。
            int idx = _imageFiles.FindIndex(
                f => string.Equals(f, dialog.FileName, StringComparison.OrdinalIgnoreCase));
            _imageIndex = idx;
            if (idx >= 0)
            {
                CurrentImageFile = Path.GetFileName(dialog.FileName);
                ImagePosition = $"{idx + 1} / {_imageFiles.Count}";
            }

            Log($"已为「{param.Name}」加载图像：{Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            Log("读取图像失败：" + ex.Message);
        }
    }

    // ---------- 执行识别 ----------

    private bool _currentImageRecognized;

    private void Execute()
    {
        // 当前图未识别过：识别并停留在本张；已识别过：切换到下一张（切图时会自动识别新图）。
        if (!_currentImageRecognized)
        {
            RecognizeCurrent();
            return;
        }

        AdvanceToNextImageAfterExecute();
    }

    /// <summary>
    /// 对“当前显示的图片”执行一次识别：更新输出参数，并把识别结果(含区域/轮廓叠加)显示到右侧。
    /// 「执行识别」按钮、以及「上一张 / 下一张 / 自动切换」刷新结果时都复用它。
    /// </summary>
    /// <returns>是否成功执行。</returns>
    private bool RecognizeCurrent()
    {
        if (_signature == null)
        {
            Log("请先加载算法文件 (.hdvp)。");
            return false;
        }

        var iconicInputs = new Dictionary<string, HObject>();
        var ctrlInputs = new Dictionary<string, HTuple>();

        try
        {
            foreach (ParamViewModel p in InputIconicParams)
            {
                if (p.IconicObject == null)
                {
                    Log($"请为图像参数「{p.Name}」选择图像（可在「图片序列」中设置图片文件夹）。");
                    return false;
                }
                iconicInputs[p.Name] = p.IconicObject;
            }

            foreach (ParamViewModel p in InputControlParams)
                ctrlInputs[p.Name] = HalconTupleHelper.Parse(p.Value);

            ExecutionOutput output = _engineService.Execute(_signature, iconicInputs, ctrlInputs);

            foreach (ParamViewModel p in OutputControlParams)
                if (output.CtrlOutputs.TryGetValue(p.Name, out HTuple? t))
                    p.Value = HalconTupleHelper.Format(t);

            foreach (ParamViewModel p in OutputIconicParams)
                if (output.IconicOutputs.TryGetValue(p.Name, out HObject? o))
                    p.Value = DescribeObject(o);

            ResultImage = HalconImageHelper.ToBitmapSource(output.DisplayImage);
            if (HalconImageHelper.LastRenderInfo is string renderInfo)
                Log("渲染：" + renderInfo);

            // 诊断：逐个打印图形输出参数的名称/类别/对象数，便于定位“没有输出”是参数缺失、
            // 未初始化还是算法本身没有检出。
            if (output.IconicOutputs.Count == 0)
                Log("该过程没有任何输出图形参数：请在 HDevelop 过程接口中把轮廓变量设为「输出图形参数」后重新导出 .hdvp。");
            foreach (var kv in output.IconicOutputs)
                Log($"输出图形参数「{kv.Key}」：{DescribeObject(kv.Value)}");

            // 显示叠加统计，帮助确认轮廓/区域确实被算法输出并绘制。
            int totalShapes = output.RegionOutputs + output.XldOutputs;
            if (totalShapes > 0)
                Log($"识别执行完成。结果图叠加：区域 {output.RegionOutputs} 个、轮廓 {output.XldOutputs} 条 (均为红色)。");
            else
                Log("识别执行完成。（本次输出中没有区域/轮廓图形对象）");

            if (totalShapes == 0)
                Log("提示：若 HDevelop 中能看到轮廓而此处没有，请确认过程已把轮廓变量设为「输出图形参数」导出；过程内部 dev_display 的显示不会传出。");

            // OK/NG 判定：输出中有任何区域/轮廓缺陷图形即判 NG。
            SetJudge(totalShapes > 0);

            // 记录到临时历史
            HistoryRecords.Insert(0, new HistoryRecord(
                DateTime.Now.ToString("HH:mm:ss"),
                string.IsNullOrEmpty(CurrentImageFile) ? "（手动加载的图）" : CurrentImageFile,
                totalShapes > 0,
                totalShapes));

            Status = "识别完成";
            _currentImageRecognized = true;
            return true;
        }
        catch (Exception ex)
        {
            Log("执行失败：" + ex.Message);
            Status = "执行失败";
            return false;
        }
    }

    private void AdvanceToNextImageAfterExecute()
    {

        // 没有设置图片文件夹时，尝试从最近加载的图片自动推断文件夹，
        // 避免出现"点了执行识别却毫无反应"的静默跳过。
        if (!TryEnsureImageSequence())
        {
            Log("未设置图片文件夹，无法自动切换下一张：请先为某个图像参数手动加载一张图片，或在「图片序列」中设置图片文件夹。");
            return;
        }

        // 当前是手动选择、且不在文件夹序列中的图片时，从文件夹第一张继续。
        int next = _imageIndex < 0 ? 0 : _imageIndex + 1;
        ShowImageAt(next, "执行完成，自动切换");
    }

    /// <summary>点击上一张/下一张时的统一入口：必要时先自动推断图片文件夹。</summary>
    private void StepImage(int delta)
    {
        if (!TryEnsureImageSequence())
        {
            Log("尚未设置图片文件夹：请先在「图片序列」中浏览/输入图片文件夹，或先为某个图像参数手动加载一张图片。");
            return;
        }

        ShowImageAt(_imageIndex + delta, delta > 0 ? "切换到下一张" : "切换到上一张");
    }

    /// <summary>
    /// 确保图片序列可用：若尚未设置文件夹，但最近加载过图片文件，
    /// 就用该图片所在目录自动作为图片文件夹，并定位到这张图。
    /// </summary>
    private bool TryEnsureImageSequence()
    {
        if (_imageFiles.Count > 0) return true;

        string? source = _lastPickedFile;
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return false;

        string? dir;
        try { dir = Path.GetDirectoryName(Path.GetFullPath(source)); }
        catch { return false; }
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;

        ImageFolder = dir;
        SaveSettings();
        RefreshImageFiles(log: true);
        if (_imageFiles.Count == 0) return false;

        _imageIndex = _imageFiles.FindIndex(
            f => string.Equals(f, source, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static string DescribeObject(HObject obj)
    {
        try
        {
            string c = HalconImageHelper.GetObjectClass(obj);
            HTuple count = new HTuple();
            HOperatorSet.CountObj(obj, out count);

            if (c == "region")
                return $"区域 (region)，共 {count.I} 个对象";
            if (c.StartsWith("xld"))
                return $"轮廓 (XLD)，共 {count.I} 个对象";
            if (string.IsNullOrEmpty(c))
                return "空对象";
            return $"{c}，共 {count.I} 个对象";
        }
        catch
        {
            return "object";
        }
    }

    private void Reset()
    {
        ClearParameters();
        _signature = null;
        AlgorithmName = "未加载";
        AlgorithmPath = string.Empty;
        ResultImage = null;
        ClearJudge();
        HistoryRecords.Clear();
        Logs.Clear();
        Status = "就绪";
        Log("已重置。请重新加载算法文件。");
    }

    private void ClearParameters()
    {
        InputIconicParams.Clear();
        InputControlParams.Clear();
        OutputIconicParams.Clear();
        OutputControlParams.Clear();
    }
}

/// <summary>一条识别历史记录（临时，内存保存）。</summary>
public class HistoryRecord
{
    public HistoryRecord(string time, string fileName, bool isNg, int defectCount)
    {
        Time = time;
        FileName = fileName;
        DefectCount = defectCount;
        Result = isNg ? "NG" : "OK";
        ResultBrush = new SolidColorBrush(isNg
            ? Color.FromRgb(0xDC, 0x26, 0x26)
            : Color.FromRgb(0x16, 0xA3, 0x4A));
    }

    public string Time { get; }
    public string FileName { get; }
    public int DefectCount { get; }
    public string Result { get; }
    public Brush ResultBrush { get; }
}
