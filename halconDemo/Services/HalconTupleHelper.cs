using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HalconDotNet;

namespace halconDemo.Services;

/// <summary>
/// 控制参数(HTuple)与界面文本之间的转换。
/// 支持：整数、浮点数、字符串、以及以逗号分隔的元组，例如 <c>128</c>、<c>1.5</c>、
/// <c>'rectangle'</c>、<c>[10, 20, 30]</c>、<c>'a','b','c'</c>。
/// </summary>
public static class HalconTupleHelper
{
    /// <summary>把界面文本解析为 HTuple。</summary>
    public static HTuple Parse(string? text)
    {
        string s = (text ?? string.Empty).Trim();
        if (s.Length == 0) return new HTuple(string.Empty);

        // 去掉最外层的中括号/花括号
        if ((s.StartsWith("[") && s.EndsWith("]")) || (s.StartsWith("{") && s.EndsWith("}")))
        {
            s = s.Substring(1, s.Length - 2).Trim();
        }
        if (s.Length == 0) return new HTuple(string.Empty);

        List<string> parts = SplitTopLevel(s);

        var ints = new List<int>();
        var doubles = new List<double>();
        var strings = new List<string>();
        bool anyString = false;
        bool allInteger = true;

        foreach (string raw in parts)
        {
            string token = raw.Trim();
            if ((token.StartsWith("'") && token.EndsWith("'") && token.Length >= 2) ||
                (token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2))
            {
                anyString = true;
                strings.Add(token.Substring(1, token.Length - 2));
            }
            else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
            {
                ints.Add(iv);
                doubles.Add(iv);
                strings.Add(token);
            }
            else if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double dv))
            {
                allInteger = false;
                doubles.Add(dv);
                strings.Add(token);
            }
            else
            {
                anyString = true;
                strings.Add(token);
            }
        }

        if (anyString)
            return new HTuple(strings.ToArray());

        if (allInteger && ints.Count == parts.Count)
            return new HTuple(ints.ToArray());

        return new HTuple(doubles.ToArray());
    }

    /// <summary>把 HTuple 结果格式化为界面显示文本。</summary>
    public static string Format(HTuple? tuple)
    {
        if (tuple == null || tuple.Length == 0) return string.Empty;

        var items = new List<string>(tuple.Length);
        for (int i = 0; i < tuple.Length; i++)
        {
            items.Add(tuple[i].ToString());
        }
        return string.Join(", ", items);
    }

    /// <summary>按顶层逗号切分，忽略引号与括号内部的分隔符。</summary>
    private static List<string> SplitTopLevel(string s)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        int depth = 0;
        char quote = '\0';

        foreach (char ch in s)
        {
            if (quote != '\0')
            {
                sb.Append(ch);
                if (ch == quote) quote = '\0';
                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                quote = ch;
                sb.Append(ch);
                continue;
            }

            if (ch == '[' || ch == '{' || ch == '(')
            {
                depth++;
                sb.Append(ch);
                continue;
            }

            if (ch == ']' || ch == '}' || ch == ')')
            {
                depth--;
                sb.Append(ch);
                continue;
            }

            if (ch == ',' && depth == 0)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        result.Add(sb.ToString());
        return result.Where(x => x.Trim().Length > 0).DefaultIfEmpty(string.Empty).ToList();
    }
}
