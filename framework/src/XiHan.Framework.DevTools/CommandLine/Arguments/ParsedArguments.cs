#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParsedArguments
// Guid:bcd0303f-3908-415c-bcbd-87f8e5f5b1fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Arguments;

/// <summary>
/// 解析后的命令行参数
/// </summary>
public class ParsedArguments
{
    /// <summary>
    /// 选项集合（键值对）
    /// </summary>
    public Dictionary<string, List<string>> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 位置参数列表
    /// </summary>
    public List<string> Arguments { get; } = [];

    /// <summary>
    /// 命令名称
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// 子命令名称
    /// </summary>
    public string? SubCommand { get; set; }

    /// <summary>
    /// 剩余的未解析参数
    /// </summary>
    public List<string> Remaining { get; } = [];

    /// <summary>
    /// 获取选项值
    /// </summary>
    /// <param name="optionName">选项名称</param>
    /// <returns>选项值</returns>
    public string? GetOption(string optionName)
    {
        return Options.TryGetValue(optionName, out var values) && values.Count > 0 ? values[0] : null;
    }

    /// <summary>
    /// 获取选项的所有值
    /// </summary>
    /// <param name="optionName">选项名称</param>
    /// <returns>选项值列表</returns>
    public List<string> GetOptions(string optionName)
    {
        return Options.TryGetValue(optionName, out var values) ? values : [];
    }

    /// <summary>
    /// 检查选项是否存在
    /// </summary>
    /// <param name="optionName">选项名称</param>
    /// <returns>是否存在</returns>
    public bool HasOption(string optionName)
    {
        return Options.ContainsKey(optionName);
    }

    /// <summary>
    /// 添加选项
    /// </summary>
    /// <param name="name">选项名称</param>
    /// <param name="value">选项值</param>
    public void AddOption(string name, string? value = null)
    {
        if (!Options.TryGetValue(name, out var values))
        {
            values = [];
            Options[name] = values;
        }

        if (value != null)
        {
            values.Add(value);
        }
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="index">参数索引</param>
    /// <returns>参数值</returns>
    public string? GetArgument(int index)
    {
        return index >= 0 && index < Arguments.Count ? Arguments[index] : null;
    }
}
