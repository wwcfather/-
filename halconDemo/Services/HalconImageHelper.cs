using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;

namespace halconDemo.Services;

/// <summary>
/// HALCON HObject 与 WPF BitmapSource 之间的转换，以及把区域/轮廓叠加到图像上用于结果显示。
/// </summary>
public static class HalconImageHelper
{
    /// <summary>最近一次 ComposeResult 渲染的诊断信息（走了哪条路径、画了几个对象、失败原因），供界面日志输出。</summary>
    public static string? LastRenderInfo { get; private set; }

    /// <summary>把 HALCON 图像转换为 WPF 可显示的 BitmapSource。灰度图输出 Gray8，彩色图输出 Bgra32。</summary>
    public static BitmapSource? ToBitmapSource(HObject? image)
    {
        if (image == null) return null;

        try
        {
            HTuple channels = new HTuple();
            HOperatorSet.CountChannels(image, out channels);
            HTuple width = new HTuple();
            HTuple height = new HTuple();
            HOperatorSet.GetImageSize(image, out width, out height);

            int w = width.I;
            int h = height.I;
            if (w <= 0 || h <= 0) return null;

            if (channels.I == 3)
            {
                HObject r, g, b;
                HOperatorSet.Decompose3(image, out r, out g, out b);

                byte[] pr = GetGrayBytes(r, out int rw, out int rh);
                byte[] pg = GetGrayBytes(g, out _, out _);
                byte[] pb = GetGrayBytes(b, out _, out _);

                int stride = rw * 4;
                byte[] pixels = new byte[stride * rh];
                for (int y = 0; y < rh; y++)
                {
                    for (int x = 0; x < rw; x++)
                    {
                        int si = y * rw + x;
                        int di = y * stride + x * 4;
                        pixels[di] = pb[si];
                        pixels[di + 1] = pg[si];
                        pixels[di + 2] = pr[si];
                        pixels[di + 3] = 255;
                    }
                }

                var color = BitmapSource.Create(rw, rh, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
                color.Freeze();
                return color;
            }
            else
            {
                // 单通道按灰度显示; 2/4 通道等特殊图像取第一通道,
                // 避免 GetImagePointer1 对多通道图像直接失败导致整图不显示。
                HObject source = image;
                if (channels.I > 1)
                {
                    try
                    {
                        HOperatorSet.AccessChannel(image, out HObject first, 1);
                        source = first;
                    }
                    catch { /* 保底用原图 */ }
                }

                byte[] buffer = GetGrayBytes(source, out int bw, out int bh);
                var gray = BitmapSource.Create(bw, bh, 96, 96, PixelFormats.Gray8, null, buffer, bw);
                gray.Freeze();
                return gray;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>取单通道图像的字节缓冲(先转换为 byte 类型)。</summary>
    private static byte[] GetGrayBytes(HObject singleChannel, out int width, out int height)
    {
        HObject work = singleChannel;

        HTuple type = new HTuple();
        HOperatorSet.GetImageType(singleChannel, out type);
        if (type.S != "byte")
        {
            HOperatorSet.ConvertImageType(singleChannel, out work, "byte");
        }

        HTuple hw = new HTuple();
        HTuple hh = new HTuple();
        HOperatorSet.GetImageSize(work, out hw, out hh);
        width = hw.I;
        height = hh.I;

        HTuple ptr = new HTuple();
        HTuple ptype = new HTuple();
        HOperatorSet.GetImagePointer1(work, out ptr, out ptype, out hw, out hh);

        int len = width * height;
        byte[] buffer = new byte[len];
        if (len > 0)
        {
            Marshal.Copy(ptr.IP, buffer, 0, len);
        }
        return buffer;
    }

    /// <summary>
    /// 判断对象是否为图像(image)。
    /// 用 CountChannels 探测:region/XLD 不是图像,调用会抛异常;
    /// 这样即使输出是"多对象元组"或空对象也不会误判或抛出。
    /// </summary>
    public static bool IsImage(HObject? obj)
    {
        if (obj == null) return false;
        try
        {
            HOperatorSet.CountObj(obj, out HTuple count);
            if (count.I == 0) return false;

            // 用 get_obj_class 判定：某些 HALCON 版本对区域/XLD 调 count_channels 也返回非空，
            // 会把轮廓误判为图像，导致叠加层被全部跳过（渲染日志"叠加了 0 个图形对象"）。
            HOperatorSet.GetObjClass(obj, out HTuple cls);
            if (cls == null || cls.Length == 0) return false;
            for (int i = 0; i < cls.Length; i++)
                if (cls[i].S != "image") return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全读取对象类别名(image/region/xld_cont 等)。
    /// 对"多对象元组"也安全:元组内元素类别一致,取第一个元素的类别即可。
    /// 读取失败(如空对象)时返回空字符串。
    /// </summary>
    public static string GetObjectClass(HObject? obj)
    {
        if (obj == null) return string.Empty;
        try
        {
            HOperatorSet.GetObjClass(obj, out HTuple cls);
            if (cls == null || cls.Length == 0) return string.Empty;
            try
            {
                return cls.S;
            }
            catch
            {
                // 多对象元组: cls.S 抛异常,改取第一个元素
                return cls[0]?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 生成用于显示的结果图：
    /// 以输入图像为彩色底图，把输出的区域(Region)叠加为红色填充、轮廓(XLD)叠加为绿色线条。
    /// 之前版本把整图先转灰度、再用 255 画白线，1 像素的轮廓在浅色背景上几乎看不见，
    /// 这是"轮廓不显示"的观感原因之一。
    /// </summary>
    public static HObject ComposeResult(HObject? baseImage, IEnumerable<HObject> overlays)
    {
        var overlayList = overlays as IList<HObject> ?? overlays.ToList();

        // 首选：用 HALCON 自己的显示算子(disp_obj + set_color/set_line_width)在一个离屏窗口里绘制，
        // 再用 dump_window_image 把窗口内容取回为图像。效果与 HDevelop 里 dev_display 完全一致：
        // 区域可填充、轮廓有线宽、颜色丰富，真正做到“把算子画出的轮廓”显示出来。
        if (baseImage != null && TryRenderWithWindow(baseImage, overlayList, out HObject windowImage))
            return windowImage;

        // 兜底：若离屏窗口不可用(个别环境/无图形库)，退回按 RGB 通道手工叠加。
        return ComposeByChannels(baseImage, overlayList);
    }

    /// <summary>
    /// 用 HALCON 显示算子渲染结果图：把底图 disp 到离屏窗口，再叠加区域(红，填充)/轮廓(绿，带线宽)，
    /// 最后 dump_window_image 取回图像。成功返回 true；失败返回 false，由调用方走兜底路径。
    /// </summary>
    private static bool TryRenderWithWindow(HObject baseImage, IEnumerable<HObject> overlays, out HObject rendered)
    {
        rendered = null!;
        HWindow? window = null;
        try
        {
            HOperatorSet.GetImageSize(baseImage, out HTuple w, out HTuple h);
            int width = w.I;
            int height = h.I;
            if (width <= 0 || height <= 0) return false;

            // 离屏("buffer")窗口：宽高与图像一致，窗口坐标即像素坐标。
            window = new HWindow(0, 0, width, height, new HTuple(0), "buffer", "");
            window.SetPart(0, 0, height - 1, width - 1);
            window.ClearWindow();
            window.DispObj(baseImage);

            int painted = 0;
            foreach (HObject obj in overlays)
            {
                if (IsImage(obj)) continue;   // 叠加层不应是整幅图像

                string c = GetObjectClass(obj);
                if (c.StartsWith("xld"))
                {
                    // 轮廓：红色、加粗，保证 1 像素细线也清晰可见。
                    window.SetColor("red");
                    window.SetLineWidth(2);
                }
                else
                {
                    // 区域：红色、填充。
                    window.SetColor("red");
                    window.SetDraw("fill");
                }
                window.DispObj(obj);
                painted++;
            }

            // 直接调算子而非 HWindow 封装方法：该版本 halcondotnet 的 DumpWindowImage 封装签名不同
            HOperatorSet.DumpWindowImage(out rendered, window);
            LastRenderInfo = $"离屏窗口渲染：叠加了 {painted} 个图形对象";
            return rendered != null;
        }
        catch (Exception ex)
        {
            LastRenderInfo = "离屏窗口渲染失败，改用通道叠加：" + ex.Message;
            rendered = null!;
            return false;
        }
        finally
        {
            window?.Dispose();
        }
    }

    /// <summary>兜底渲染：按 R/G/B 通道手工叠加区域(红)/轮廓(绿)，不依赖图形窗口。</summary>
    private static HObject ComposeByChannels(HObject? baseImage, IEnumerable<HObject> overlays)
    {
        // 底图三个通道：优先保留彩色；灰度图复制成三个相同通道，最后合并为 RGB。
        HObject rCh, gCh, bCh;

        if (baseImage != null)
        {
            try
            {
                HOperatorSet.CountChannels(baseImage, out HTuple ch);
                if (ch.I == 3)
                {
                    HOperatorSet.Decompose3(baseImage, out HObject r0, out HObject g0, out HObject b0);
                    rCh = ToByte(r0);
                    gCh = ToByte(g0);
                    bCh = ToByte(b0);
                }
                else
                {
                    // 单通道 / 其他通道数:取灰度或第一通道
                    HObject source = baseImage;
                    if (ch.I > 1)
                    {
                        try
                        {
                            HOperatorSet.AccessChannel(baseImage, out HObject first, 1);
                            source = first;
                        }
                        catch { /* 保底用原图转换 */ }
                    }
                    HOperatorSet.ConvertImageType(source, out HObject gray, "byte");
                    HOperatorSet.CopyImage(gray, out rCh);
                    HOperatorSet.CopyImage(gray, out gCh);
                    HOperatorSet.CopyImage(gray, out bCh);
                }
            }
            catch
            {
                CreateBlankCanvas(out rCh, out gCh, out bCh);
            }
        }
        else
        {
            CreateBlankCanvas(out rCh, out gCh, out bCh);
        }

        int regionsPainted = 0, xldsPainted = 0, skipped = 0;
        foreach (HObject obj in overlays)
        {
            // 空对象元组(如过程中 gen_empty_obj 后未 concat 任何结果的输出)会让 paint_region
            // 产出 0 个图像对象，之后 compose3 报错 1502；过程声明了输出但从未赋值时
            // 引擎返回未初始化对象(ID 为 NULL)，count_obj 会报 4056。两种情况都直接跳过。
            try
            {
                HOperatorSet.CountObj(obj, out HTuple objCount);
                if (objCount.I == 0) continue;
            }
            catch
            {
                continue;
            }

            // 不再仅依赖 get_obj_class 的字符串判断(不同 HALCON 版本或对象元组下可能取不到类别，
            // 会导致区域/轮廓被整段跳过、结果图上只剩原图)。改为“先按区域画红色，失败再按轮廓画绿色”，
            // 两者都失败才跳过，保证算法输出的图形对象一定能画出来。
            if (TryPaintRegion(obj, ref rCh))
                regionsPainted++;
            else if (TryPaintXld(obj, ref rCh))   // 轮廓同样画红色
                xldsPainted++;
            else
                skipped++;
        }

        LastRenderInfo += $"；通道叠加：区域 {regionsPainted} 个、轮廓 {xldsPainted} 个、跳过 {skipped} 个";

        HOperatorSet.Compose3(rCh, gCh, bCh, out HObject rgb);
        return rgb;
    }

    /// <summary>把区域以红色填充画到指定通道；失败返回 false。</summary>
    private static bool TryPaintRegion(HObject region, ref HObject channel)
    {
        try
        {
            HOperatorSet.PaintRegion(region, channel, out HObject painted, 255, "fill");
            channel.Dispose();
            channel = painted;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>把轮廓(XLD)以绿色画到指定通道；先加粗再画，保证细线在结果图上清晰可见。</summary>
    private static bool TryPaintXld(HObject xld, ref HObject channel)
    {
        try
        {
            HOperatorSet.GenRegionContourXld(xld, out HObject region, "margin");
            HOperatorSet.DilationCircle(region, out HObject thick, 1.5);
            HOperatorSet.PaintRegion(thick, channel, out HObject painted, 255, "fill");
            channel.Dispose();
            channel = painted;
            return true;
        }
        catch
        {
            try
            {
                HOperatorSet.PaintXld(xld, channel, out HObject painted, 255);
                channel.Dispose();
                channel = painted;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static void CreateBlankCanvas(out HObject rCh, out HObject gCh, out HObject bCh)
    {
        HOperatorSet.GenImageConst(out rCh, "byte", 640, 480);
        HOperatorSet.CopyImage(rCh, out gCh);
        HOperatorSet.CopyImage(rCh, out bCh);
    }

    /// <summary>把单通道图像转换为 byte 类型(已是 byte 则原样返回)。</summary>
    private static HObject ToByte(HObject single)
    {
        try
        {
            HOperatorSet.GetImageType(single, out HTuple t);
            if (t.S == "byte") return single;

            HOperatorSet.ConvertImageType(single, out HObject converted, "byte");
            return converted;
        }
        catch
        {
            return single;
        }
    }
}
