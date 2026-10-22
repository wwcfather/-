namespace halconDemo.Models;

/// <summary>参数种类：控制参数(数值/字符串等) 或 图标参数(图像/区域/轮廓)。</summary>
public enum ParamKind
{
    Control,
    Iconic
}

/// <summary>参数方向：输入 或 输出。</summary>
public enum ParamDirection
{
    Input,
    Output
}

/// <summary>算法接口中的一个参数描述(与具体 UI 无关)。</summary>
public class ParamDescriptor
{
    public string Name { get; set; } = string.Empty;
    public ParamKind Kind { get; set; }
    public ParamDirection Direction { get; set; }
    public string Value { get; set; } = string.Empty;

    public ParamDescriptor() { }

    public ParamDescriptor(string name, ParamKind kind, ParamDirection direction, string value = "")
    {
        Name = name;
        Kind = kind;
        Direction = direction;
        Value = value;
    }
}
