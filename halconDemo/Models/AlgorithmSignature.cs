using System.Collections.Generic;
using System.Linq;

namespace halconDemo.Models;

/// <summary>
/// 算法接口签名：描述一个 HDevelop 导出算法的全部输入/输出参数。
/// UI 依据该签名动态生成参数录入区。
/// </summary>
public class AlgorithmSignature
{
    /// <summary>算法文件绝对路径。</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>算法名称(过程名)。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>过程所在目录(用于设置 HDevEngine 的过程搜索路径)。</summary>
    public string BaseDirectory { get; set; } = string.Empty;

    /// <summary>是否为 HALCON 过程(.hdvp)。</summary>
    public bool IsProcedure { get; set; }

    public List<ParamDescriptor> Inputs { get; } = new();
    public List<ParamDescriptor> Outputs { get; } = new();

    public IEnumerable<ParamDescriptor> InputIconics =>
        Inputs.Where(p => p.Kind == ParamKind.Iconic);

    public IEnumerable<ParamDescriptor> InputControls =>
        Inputs.Where(p => p.Kind == ParamKind.Control);

    public IEnumerable<ParamDescriptor> OutputIconics =>
        Outputs.Where(p => p.Kind == ParamKind.Iconic);

    public IEnumerable<ParamDescriptor> OutputControls =>
        Outputs.Where(p => p.Kind == ParamKind.Control);
}
