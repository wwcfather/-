using System.Windows.Media.Imaging;
using HalconDotNet;
using halconDemo.Models;

namespace halconDemo.ViewModels;

/// <summary>界面上的一个参数项：绑定输入值、图像缩略图与输出结果。</summary>
public class ParamViewModel : ObservableObject
{
    public string Name { get; }
    public ParamKind Kind { get; }
    public ParamDirection Direction { get; }

    public bool IsIconic => Kind == ParamKind.Iconic;
    public bool IsControl => Kind == ParamKind.Control;
    public bool IsInput => Direction == ParamDirection.Input;
    public bool IsOutput => Direction == ParamDirection.Output;

    private string _value = string.Empty;
    /// <summary>控制参数的文本值(可编辑)，或输出的显示文本。</summary>
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    private string _filePath = string.Empty;
    /// <summary>图标输入参数对应的图像文件路径。</summary>
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    private BitmapSource? _thumbnail;
    /// <summary>图标输入参数的图像缩略图。</summary>
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>输入图标参数实际加载的 HALCON 对象。</summary>
    public HObject? IconicObject { get; set; }

    public ParamViewModel(ParamDescriptor descriptor)
    {
        Name = descriptor.Name;
        Kind = descriptor.Kind;
        Direction = descriptor.Direction;
        Value = descriptor.Value;
    }
}
