#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleTable
// Guid:b1f45c21-d35f-4a29-84b7-28fd84218122
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/15 12:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 控制台表格，支持自适应宽度、高度和多种边框样式
/// </summary>
public class ConsoleTable
{
    private readonly List<string> _headers = [];
    private readonly List<List<string>> _rows = [];
    private readonly List<int> _columnWidths = [];
    private readonly List<ConsoleColor?> _headerColors = [];
    private readonly List<List<ConsoleColor?>> _rowColors = [];

    /// <summary>
    /// 创建一个新的控制台表格
    /// </summary>
    public ConsoleTable()
    {
    }

    /// <summary>
    /// 创建一个新的控制台表格并设置表头
    /// </summary>
    /// <param name="headers">表头</param>
    public ConsoleTable(params string[] headers) : this()
    {
        SetHeaders(headers);
    }

    /// <summary>
    /// 边框样式，默认为简单样式
    /// </summary>
    public ConsoleTableBorderStyle BorderStyle { get; set; } = ConsoleTableBorderStyle.Simple;

    /// <summary>
    /// 是否显示表头
    /// </summary>
    public bool ShowHeaders { get; set; } = true;

    /// <summary>
    /// 最小列宽
    /// </summary>
    public int MinColumnWidth { get; set; } = 3;

    /// <summary>
    /// 最大列宽，0 表示无限制
    /// </summary>
    public int MaxColumnWidth { get; set; } = 0;

    /// <summary>
    /// 内边距
    /// </summary>
    public int Padding { get; set; } = 1;

    /// <summary>
    /// 默认文本颜色
    /// </summary>
    public ConsoleColor? DefaultTextColor { get; set; }

    /// <summary>
    /// 默认表头颜色
    /// </summary>
    public ConsoleColor? DefaultHeaderColor { get; set; } = ConsoleColor.Yellow;

    /// <summary>
    /// 是否显示行分隔线
    /// </summary>
    public bool ShowRowSeparators { get; set; } = false;

    /// <summary>
    /// 设置表头
    /// </summary>
    /// <param name="headers">表头数组</param>
    /// <returns>当前表格实例，支持链式调用</returns>
    public ConsoleTable SetHeaders(params string[] headers)
    {
        _headers.Clear();
        _headerColors.Clear();

        foreach (var header in headers)
        {
            _headers.Add(header ?? string.Empty);
            _headerColors.Add(DefaultHeaderColor);
        }

        RecalculateColumnWidths();
        return this;
    }

    /// <summary>
    /// 设置表头并指定颜色
    /// </summary>
    /// <param name="headersWithColors">表头和颜色的元组数组</param>
    /// <returns>当前表格实例，支持链式调用</returns>
    public ConsoleTable SetHeaders(params (string header, ConsoleColor? color)[] headersWithColors)
    {
        _headers.Clear();
        _headerColors.Clear();

        foreach (var (header, color) in headersWithColors)
        {
            _headers.Add(header ?? string.Empty);
            _headerColors.Add(color ?? DefaultHeaderColor);
        }

        RecalculateColumnWidths();
        return this;
    }

    /// <summary>
    /// 添加一行数据
    /// </summary>
    /// <param name="values">行数据</param>
    /// <returns>当前表格实例，支持链式调用</returns>
    public ConsoleTable AddRow(params object[] values)
    {
        var rowData = values.Select(v => v?.ToString() ?? string.Empty).ToList();
        var rowColors = Enumerable.Repeat(DefaultTextColor, values.Length).ToList();

        // 确保行数据长度与表头一致
        while (rowData.Count < _headers.Count)
        {
            rowData.Add(string.Empty);
            rowColors.Add(DefaultTextColor);
        }

        _rows.Add(rowData);
        _rowColors.Add(rowColors);

        RecalculateColumnWidths();
        return this;
    }

    /// <summary>
    /// 添加一行数据并指定颜色
    /// </summary>
    /// <param name="valuesWithColors">值和颜色的元组数组</param>
    /// <returns>当前表格实例，支持链式调用</returns>
    public ConsoleTable AddRow(params (object value, ConsoleColor? color)[] valuesWithColors)
    {
        var rowData = valuesWithColors.Select(v => v.value?.ToString() ?? string.Empty).ToList();
        var rowColors = valuesWithColors.Select(v => v.color ?? DefaultTextColor).ToList();

        // 确保行数据长度与表头一致
        while (rowData.Count < _headers.Count)
        {
            rowData.Add(string.Empty);
            rowColors.Add(DefaultTextColor);
        }

        _rows.Add(rowData);
        _rowColors.Add(rowColors);

        RecalculateColumnWidths();
        return this;
    }

    /// <summary>
    /// 添加多行数据
    /// </summary>
    /// <param name="rows">行数据集合</param>
    /// <returns>当前表格实例，支持链式调用</returns>
    public ConsoleTable AddRows(IEnumerable<object[]> rows)
    {
        foreach (var row in rows)
        {
            AddRow(row);
        }
        return this;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    /// <returns>当前表格实例，支持链式调用</returns>
    public ConsoleTable Clear()
    {
        _headers.Clear();
        _headerColors.Clear();
        _rows.Clear();
        _rowColors.Clear();
        _columnWidths.Clear();
        return this;
    }

    /// <summary>
    /// 清空行数据，保留表头
    /// </summary>
    /// <returns>当前表格实例，支持链式调用</returns>
    public ConsoleTable ClearRows()
    {
        _rows.Clear();
        _rowColors.Clear();
        RecalculateColumnWidths();
        return this;
    }

    /// <summary>
    /// 打印带颜色的表格到控制台
    /// </summary>
    public void Print()
    {
        if (_headers.Count == 0)
        {
            return;
        }

        var borderChars = ConsoleTableBorderChars.GetBorderChars(BorderStyle);

        // 绘制顶部边框
        if (BorderStyle != ConsoleTableBorderStyle.None)
        {
            Console.WriteLine(BuildHorizontalLine(borderChars, true, false));
        }

        // 绘制表头
        if (ShowHeaders)
        {
            PrintRowLines(_headers, _headerColors, borderChars);

            // 绘制表头分隔线
            if (BorderStyle != ConsoleTableBorderStyle.None)
            {
                Console.WriteLine(BuildHorizontalLine(borderChars, false, false));
            }
        }

        // 绘制数据行
        for (var i = 0; i < _rows.Count; i++)
        {
            PrintRowLines(_rows[i], _rowColors[i], borderChars);

            // 在行之间添加分隔线（如果启用且不是最后一行）
            if (ShowRowSeparators && BorderStyle != ConsoleTableBorderStyle.None && i < _rows.Count - 1)
            {
                Console.WriteLine(BuildHorizontalLine(borderChars, false, false));
            }
        }

        // 绘制底部边框
        if (BorderStyle != ConsoleTableBorderStyle.None)
        {
            Console.WriteLine(BuildHorizontalLine(borderChars, false, true));
        }
    }

    /// <summary>
    /// 输出表格到控制台并换行
    /// </summary>
    public void PrintLine()
    {
        Print();
        Console.WriteLine();
    }

    /// <summary>
    /// 获取表格字符串
    /// </summary>
    /// <returns>表格字符串</returns>
    public override string ToString()
    {
        return BuildTable();
    }

    #region 快捷方法

    /// <summary>
    /// 快速创建简单表格
    /// </summary>
    /// <param name="headers">表头</param>
    /// <param name="rows">行数据</param>
    /// <param name="style">边框样式</param>
    public static ConsoleTable QuickTable(
        string[] headers,
        object[][] rows,
        ConsoleTableBorderStyle style = ConsoleTableBorderStyle.Simple)
    {
        var table = new ConsoleTable(headers)
        {
            BorderStyle = style
        };

        foreach (var row in rows)
        {
            table.AddRow(row);
        }

        return table;
    }

    /// <summary>
    /// 键值对表格
    /// </summary>
    /// <param name="data">键值对数据</param>
    /// <param name="keyHeader">键的表头名称</param>
    /// <param name="valueHeader">值的表头名称</param>
    /// <param name="style">边框样式</param>
    public static ConsoleTable KeyValueTable(
        IDictionary<string, object> data,
        string keyHeader = "Key",
        string valueHeader = "Value",
        ConsoleTableBorderStyle style = ConsoleTableBorderStyle.Simple)
    {
        var table = new ConsoleTable(keyHeader, valueHeader)
        {
            BorderStyle = style
        };

        foreach (var kvp in data)
        {
            table.AddRow(kvp.Key, kvp.Value?.ToString() ?? "null");
        }

        return table;
    }

    /// <summary>
    /// 输出对象属性表格
    /// </summary>
    /// <param name="obj">对象</param>
    /// <param name="propertyHeader">属性表头名称</param>
    /// <param name="valueHeader">值表头名称</param>
    /// <param name="style">边框样式</param>
    public static ConsoleTable ObjectTable(
        object obj,
        string propertyHeader = "Property",
        string valueHeader = "Value",
        ConsoleTableBorderStyle style = ConsoleTableBorderStyle.Simple)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var table = new ConsoleTable(propertyHeader, valueHeader)
        {
            BorderStyle = style
        };

        var properties = obj.GetType().GetProperties();
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(obj);
                table.AddRow(prop.Name, value?.ToString() ?? "null");
            }
            catch (Exception ex)
            {
                table.AddRow(prop.Name, $"Error: {ex.Message}");
            }
        }

        return table;
    }

    #endregion

    #region 内部方法

    /// <summary>
    /// 查找合适的分割位置（考虑显示宽度）
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="maxWidth">最大显示宽度</param>
    /// <returns>分割位置（字符索引）</returns>
    private static int FindSplitIndex(string text, int maxWidth)
    {
        if (GetDisplayWidth(text) <= maxWidth)
        {
            return text.Length;
        }

        var currentWidth = 0;
        var lastSpaceIndex = -1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // 跳过控制字符
            if (char.IsControl(c))
            {
                continue;
            }

            var charWidth = IsFullWidth(c) ? 2 : 1;

            if (currentWidth + charWidth > maxWidth)
            {
                // 如果找到了空格位置且不会造成过短的分割，在空格处分割
                if (lastSpaceIndex >= 0 && lastSpaceIndex >= i / 2)
                {
                    return lastSpaceIndex;
                }
                // 否则在当前位置分割
                return i;
            }

            if (char.IsWhiteSpace(c))
            {
                lastSpaceIndex = i;
            }

            currentWidth += charWidth;
        }

        return text.Length;
    }

    /// <summary>
    /// 填充内容到指定宽度
    /// </summary>
    /// <param name="content">内容</param>
    /// <param name="width">宽度</param>
    /// <returns>填充后的内容</returns>
    private static string PadContent(string content, int width)
    {
        var actualWidth = GetDisplayWidth(content);
        if (actualWidth > width)
        {
            // 需要截取内容以适应指定宽度
            return TruncateToWidth(content, width);
        }

        if (actualWidth == width)
        {
            return content;
        }

        var padding = width - actualWidth;
        return content + new string(' ', padding);
    }

    /// <summary>
    /// 根据显示宽度截取文本
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="maxWidth">最大显示宽度</param>
    /// <returns>截取后的文本</returns>
    private static string TruncateToWidth(string text, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var currentWidth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // 跳过控制字符
            if (char.IsControl(c))
            {
                continue;
            }

            var charWidth = IsFullWidth(c) ? 2 : 1;

            if (currentWidth + charWidth > maxWidth)
            {
                return text[..i];
            }

            currentWidth += charWidth;
        }

        return text;
    }

    /// <summary>
    /// 获取文本的显示宽度（考虑中文字符和特殊字符）
    /// </summary>
    /// <param name="text">文本</param>
    /// <returns>显示宽度</returns>
    private static int GetDisplayWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var width = 0;
        foreach (var c in text)
        {
            // 控制字符和不可见字符不占宽度
            if (char.IsControl(c))
            {
                continue;
            }

            // 全角字符（包括中文、日文、韩文等）宽度为2
            if (IsFullWidth(c))
            {
                width += 2;
            }
            else
            {
                width += 1;
            }
        }
        return width;
    }

    /// <summary>
    /// 判断字符是否为全角字符
    /// </summary>
    /// <param name="c">字符</param>
    /// <returns>是否为全角字符</returns>
    private static bool IsFullWidth(char c)
    {
        // 中文字符范围
        if (c is >= (char)0x4E00 and <= (char)0x9FFF)
        {
            return true;
        }

        // 全角字符范围
        if (c is >= (char)0xFF01 and <= (char)0xFF60)
        {
            return true;
        }

        // 其他常见全角字符范围
        if (c is >= (char)0x3000 and <= (char)0x303F) // CJK符号和标点
        {
            return true;
        }

        if (c is >= (char)0x3040 and <= (char)0x309F) // 平假名
        {
            return true;
        }

        if (c is >= (char)0x30A0 and <= (char)0x30FF) // 片假名
        {
            return true;
        }

        if (c is >= (char)0xAC00 and <= (char)0xD7AF) // 韩文
        {
            return true;
        }

        // Emoji 和其他宽字符（简化判断）
        if (c >= 0x1F600)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 重新计算列宽
    /// </summary>
    private void RecalculateColumnWidths()
    {
        if (_headers.Count == 0)
        {
            return;
        }

        _columnWidths.Clear();

        for (var col = 0; col < _headers.Count; col++)
        {
            var maxWidth = MinColumnWidth;

            // 检查表头宽度
            if (ShowHeaders && col < _headers.Count)
            {
                maxWidth = Math.Max(maxWidth, GetDisplayWidth(_headers[col]));
            }

            // 检查每行数据的宽度
            foreach (var row in _rows)
            {
                if (col < row.Count)
                {
                    var lines = SplitToLines(row[col]);
                    foreach (var line in lines)
                    {
                        maxWidth = Math.Max(maxWidth, GetDisplayWidth(line));
                    }
                }
            }

            // 应用最大宽度限制
            if (MaxColumnWidth > 0 && maxWidth > MaxColumnWidth)
            {
                maxWidth = MaxColumnWidth;
            }

            _columnWidths.Add(maxWidth);
        }
    }

    /// <summary>
    /// 构建表格字符串
    /// </summary>
    /// <returns>表格字符串</returns>
    private string BuildTable()
    {
        if (_headers.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var borderChars = ConsoleTableBorderChars.GetBorderChars(BorderStyle);

        // 绘制顶部边框
        if (BorderStyle != ConsoleTableBorderStyle.None)
        {
            sb.AppendLine(BuildHorizontalLine(borderChars, true, false));
        }

        // 绘制表头
        if (ShowHeaders)
        {
            BuildRowLines(_headers, sb, borderChars);

            // 绘制表头分隔线
            if (BorderStyle != ConsoleTableBorderStyle.None)
            {
                sb.AppendLine(BuildHorizontalLine(borderChars, false, false));
            }
        }

        // 绘制数据行
        for (var i = 0; i < _rows.Count; i++)
        {
            BuildRowLines(_rows[i], sb, borderChars);

            // 在行之间添加分隔线（如果启用且不是最后一行）
            if (ShowRowSeparators && BorderStyle != ConsoleTableBorderStyle.None && i < _rows.Count - 1)
            {
                sb.AppendLine(BuildHorizontalLine(borderChars, false, false));
            }
        }

        // 绘制底部边框
        if (BorderStyle != ConsoleTableBorderStyle.None)
        {
            sb.AppendLine(BuildHorizontalLine(borderChars, false, true));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 构建一行或多行（处理换行的情况）
    /// </summary>
    /// <param name="rowData">行数据</param>
    /// <param name="sb">字符串构建器</param>
    /// <param name="borderChars">边框字符集</param>
    private void BuildRowLines(List<string> rowData, StringBuilder sb, ConsoleTableBorderCharSet borderChars)
    {
        // 将每列的内容分割成多行
        var columnLines = new List<List<string>>();
        var maxLines = 1;

        for (var col = 0; col < _headers.Count; col++)
        {
            var cellContent = col < rowData.Count ? rowData[col] : string.Empty;
            var lines = SplitToLines(cellContent);
            columnLines.Add(lines);
            maxLines = Math.Max(maxLines, lines.Count);
        }

        // 输出每一行
        for (var lineIndex = 0; lineIndex < maxLines; lineIndex++)
        {
            if (BorderStyle != ConsoleTableBorderStyle.None)
            {
                sb.Append(borderChars.Vertical);
            }

            for (var col = 0; col < _headers.Count; col++)
            {
                var lines = columnLines[col];
                var content = lineIndex < lines.Count ? lines[lineIndex] : string.Empty;
                var paddedContent = PadContent(content, _columnWidths[col]);

                sb.Append(new string(' ', Padding));
                sb.Append(paddedContent);
                sb.Append(new string(' ', Padding));

                if (BorderStyle != ConsoleTableBorderStyle.None)
                {
                    sb.Append(borderChars.Vertical);
                }
            }

            sb.AppendLine();
        }
    }

    /// <summary>
    /// 构建水平分隔线
    /// </summary>
    /// <param name="borderChars">边框字符集</param>
    /// <param name="isTop">是否是顶部</param>
    /// <param name="isBottom">是否是底部</param>
    /// <returns>水平分隔线</returns>
    private string BuildHorizontalLine(ConsoleTableBorderCharSet borderChars, bool isTop, bool isBottom)
    {
        var sb = new StringBuilder();

        // 左端字符
        if (isTop)
        {
            sb.Append(borderChars.TopLeft);
        }
        else if (isBottom)
        {
            sb.Append(borderChars.BottomLeft);
        }
        else
        {
            sb.Append(borderChars.LeftCenter);
        }

        for (var col = 0; col < _columnWidths.Count; col++)
        {
            // 列内容区域
            var lineLength = _columnWidths[col] + (Padding * 2);
            sb.Append(new string(borderChars.Horizontal, lineLength));

            // 列分隔符
            if (col < _columnWidths.Count - 1)
            {
                if (isTop)
                {
                    sb.Append(borderChars.TopCenter);
                }
                else if (isBottom)
                {
                    sb.Append(borderChars.BottomCenter);
                }
                else
                {
                    sb.Append(borderChars.Cross);
                }
            }
        }

        // 右端字符
        if (isTop)
        {
            sb.Append(borderChars.TopRight);
        }
        else if (isBottom)
        {
            sb.Append(borderChars.BottomRight);
        }
        else
        {
            sb.Append(borderChars.RightCenter);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 将文本按最大宽度分割成多行
    /// </summary>
    /// <param name="text">文本</param>
    /// <returns>分割后的文本</returns>
    private List<string> SplitToLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [string.Empty];
        }

        // 首先按换行符分割
        var naturalLines = text.Split('\n');
        var result = new List<string>();

        foreach (var line in naturalLines)
        {
            if (MaxColumnWidth > 0 && GetDisplayWidth(line) > MaxColumnWidth)
            {
                // 需要进一步分割
                var currentLine = line;
                while (GetDisplayWidth(currentLine) > MaxColumnWidth)
                {
                    var splitIndex = FindSplitIndex(currentLine, MaxColumnWidth);
                    var part = currentLine[..splitIndex].TrimEnd();
                    result.Add(part);

                    // 移除已处理的部分，并去掉开头的空白字符
                    currentLine = currentLine[splitIndex..].TrimStart();
                }
                if (!string.IsNullOrEmpty(currentLine))
                {
                    result.Add(currentLine);
                }
            }
            else
            {
                result.Add(line);
            }
        }

        return result;
    }

    /// <summary>
    /// 打印一行或多行到控制台（带颜色支持）
    /// </summary>
    /// <param name="rowData">行数据</param>
    /// <param name="colors">颜色</param>
    /// <param name="borderChars">边框字符集</param>
    private void PrintRowLines(List<string> rowData, List<ConsoleColor?> colors, ConsoleTableBorderCharSet borderChars)
    {
        // 将每列的内容分割成多行
        var columnLines = new List<List<string>>();
        var maxLines = 1;

        for (var col = 0; col < _headers.Count; col++)
        {
            var cellContent = col < rowData.Count ? rowData[col] : string.Empty;
            var lines = SplitToLines(cellContent);
            columnLines.Add(lines);
            maxLines = Math.Max(maxLines, lines.Count);
        }

        // 输出每一行
        for (var lineIndex = 0; lineIndex < maxLines; lineIndex++)
        {
            // 输出左边框
            if (BorderStyle != ConsoleTableBorderStyle.None)
            {
                Console.Write(borderChars.Vertical);
            }

            for (var col = 0; col < _headers.Count; col++)
            {
                var lines = columnLines[col];
                var content = lineIndex < lines.Count ? lines[lineIndex] : string.Empty;

                // 确保内容被正确填充到列宽
                var paddedContent = PadContent(content, _columnWidths[col]);
                var cellColor = col < colors.Count ? colors[col] : DefaultTextColor;

                // 输出左内边距
                Console.Write(new string(' ', Padding));

                // 输出带颜色的内容
                if (cellColor.HasValue)
                {
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = cellColor.Value;
                    Console.Write(paddedContent);
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    Console.Write(paddedContent);
                }

                // 输出右内边距
                Console.Write(new string(' ', Padding));

                // 输出右边框
                if (BorderStyle != ConsoleTableBorderStyle.None)
                {
                    Console.Write(borderChars.Vertical);
                }
            }

            Console.WriteLine();
        }
    }

    #endregion
}
