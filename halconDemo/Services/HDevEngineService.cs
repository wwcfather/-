using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HalconDotNet;
using halconDemo.Models;

namespace halconDemo.Services;

/// <summary>一次算法执行的输出结果。</summary>
public class ExecutionOutput
{
    public Dictionary<string, HTuple> CtrlOutputs { get; } = new();
    public Dictionary<string, HObject> IconicOutputs { get; } = new();
    public HObject? DisplayImage { get; set; }
    /// <summary>输出的区域对象总数（用于界面日志提示）。</summary>
    public int RegionOutputs { get; set; }
    /// <summary>输出的 XLD 轮廓总数（用于界面日志提示）。</summary>
    public int XldOutputs { get; set; }
}

/// <summary>
/// HDevEngine 封装：负责加载 HALCON 导出的过程算法、解析接口、并在运行时执行。
/// </summary>
public class HDevEngineService : IDisposable
{
    private readonly HDevEngine _engine;
    private HDevProcedure? _procedure;
    private AlgorithmSignature? _signature;

    public HDevEngineService()
    {
        _engine = new HDevEngine();
    }

    public AlgorithmSignature? Signature => _signature;

    /// <summary>
    /// 加载 HALCON 导出的过程文件(.hdvp)，并自动解析其输入/输出接口。
    /// </summary>
    public AlgorithmSignature LoadProcedure(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(filePath);

        _engine.SetProcedurePath(dir);
        _procedure = new HDevProcedure(name);

        var signature = new AlgorithmSignature
        {
            FilePath = filePath,
            Name = name,
            BaseDirectory = dir,
            IsProcedure = true
        };

        ReadProcedureInterface(_procedure, signature);
        _signature = signature;
        return signature;
    }

    /// <summary>从过程对象读取接口描述，填充到签名。</summary>
    private static void ReadProcedureInterface(HDevProcedure proc, AlgorithmSignature signature)
    {
        // 注: 不同 HALCON 版本的 .NET 接口里, 参数名下标有 0 起始与 1 起始两种约定,
        // 先按 0 起始读取, 遇到 Bad index 再自动按 1 起始重试, 两种版本都能兼容。
        ReadParamGroup(signature.Inputs, "输入图像参数",
            proc.GetInputIconicParamCount, proc.GetInputIconicParamName,
            ParamKind.Iconic, ParamDirection.Input);
        ReadParamGroup(signature.Inputs, "输入控制参数",
            proc.GetInputCtrlParamCount, proc.GetInputCtrlParamName,
            ParamKind.Control, ParamDirection.Input);
        ReadParamGroup(signature.Outputs, "输出图像参数",
            proc.GetOutputIconicParamCount, proc.GetOutputIconicParamName,
            ParamKind.Iconic, ParamDirection.Output);
        ReadParamGroup(signature.Outputs, "输出控制参数",
            proc.GetOutputCtrlParamCount, proc.GetOutputCtrlParamName,
            ParamKind.Control, ParamDirection.Output);
    }

    /// <summary>读取一组参数名: 先按 0 起始下标尝试, 失败则按 1 起始重试。</summary>
    private static void ReadParamGroup(
        List<ParamDescriptor> target,
        string groupTitle,
        Func<int> countOf,
        Func<int, string> nameOf,
        ParamKind kind,
        ParamDirection direction)
    {
        int count = countOf();
        if (count <= 0) return;

        List<string> names;
        try
        {
            names = new List<string>(count);
            for (int i = 0; i < count; i++)
                names.Add(nameOf(i));
        }
        catch (HDevEngineException)
        {
            // 0 起始失败, 改按 1 起始重试
            names = new List<string>(count);
            try
            {
                for (int i = 1; i <= count; i++)
                    names.Add(nameOf(i));
            }
            catch (HDevEngineException ex)
            {
                throw new InvalidOperationException(
                    $"读取过程接口的{groupTitle}失败(共 {count} 个, 已自动尝试 0 起始与 1 起始两种下标): {ex.Message}", ex);
            }
        }

        foreach (string name in names)
            target.Add(new ParamDescriptor(name, kind, direction));
    }

    /// <summary>执行算法。</summary>
    public ExecutionOutput Execute(
        AlgorithmSignature signature,
        Dictionary<string, HObject> iconicInputs,
        Dictionary<string, HTuple> ctrlInputs)
    {
        if (_procedure == null)
            throw new InvalidOperationException("尚未加载任何算法过程。");

        HDevProcedureCall call = _procedure.CreateCall();

        foreach (var kv in iconicInputs)
            call.SetInputIconicParamObject(kv.Key, kv.Value);

        foreach (var kv in ctrlInputs)
            call.SetInputCtrlParamTuple(kv.Key, kv.Value);

        call.Execute();

        var output = new ExecutionOutput();

        foreach (ParamDescriptor p in signature.Outputs)
        {
            if (p.Kind == ParamKind.Control)
            {
                output.CtrlOutputs[p.Name] = call.GetOutputCtrlParamTuple(p.Name);
            }
            else
            {
                output.IconicOutputs[p.Name] = call.GetOutputIconicParamObject(p.Name);
            }
        }

        // 选择显示底图：
        //  - 若算法输出了区域/轮廓等图形对象，则在“输入原图”上叠加绘制，识别结果一目了然；
        //  - 若算法没有输出图形对象、但直接输出了一张结果图像(例如算法内部自己绘制后导出为输出图像)，
        //    则直接显示这张结果图像，而不是丢掉它、只显示原图。
        HObject? inputImage = iconicInputs.Values.FirstOrDefault();
        HObject? outputImage = null;

        var overlays = new List<HObject>();
        foreach (var kv in output.IconicOutputs)
        {
            // 注意:这里用 IsImage 判断(内部基于 CountChannels)，对“多对象元组”和空对象都安全。
            if (HalconImageHelper.IsImage(kv.Value))
            {
                outputImage ??= kv.Value;
            }
            else
            {
                overlays.Add(kv.Value);

                // 统计叠加对象数量,便于在界面上确认轮廓/区域确实输出了。
                try
                {
                    HOperatorSet.CountObj(kv.Value, out HTuple n);
                    string c = HalconImageHelper.GetObjectClass(kv.Value);
                    if (c.StartsWith("xld"))
                        output.XldOutputs += n.I;
                    else
                        output.RegionOutputs += n.I;   // 类别读不出时按区域计入，避免统计为 0
                }
                catch { /* 统计失败不影响显示 */ }
            }
        }

        // 选择显示底图：
        //  优先用“输入原图”；若算法没有声明图像输入(例如内部用 read_image 读取固定路径，
        //  再把读入的图作为“输出图像”导出)，则退回用算法输出的图像作为底图。
        //  关键：不能因为“有区域/轮廓输出”就把底图固定成可能为空的输入图——
        //  否则底图为空时会退化成一张黑色画布，结果图整片变黑(这正是“识别结果是黑的”的原因)。
        HObject? displayBase = inputImage ?? outputImage;

        output.DisplayImage = HalconImageHelper.ComposeResult(displayBase, overlays);
        return output;
    }

    public void Dispose()
    {
        _procedure = null;
        _signature = null;
    }
}
