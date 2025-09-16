#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParsedArguments
// Guid:g3h4i568-f0h7-9384-di2g-e4fd84c18140
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.CommandLine.Models;

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

/// <summary>
/// 参数解析配置
/// </summary>
public class ParseOptions
{
    /// <summary>
    /// 是否允许未知选项
    /// </summary>
    public bool AllowUnknownOptions { get; set; } = false;

    /// <summary>
    /// 是否大小写敏感
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// 是否启用POSIX样式（单个-后面可以跟多个短选项）
    /// </summary>
    public bool EnablePosixStyle { get; set; } = true;

    /// <summary>
    /// 是否自动生成帮助选项
    /// </summary>
    public bool AutoGenerateHelp { get; set; } = true;

    /// <summary>
    /// 是否自动生成版本选项
    /// </summary>
    public bool AutoGenerateVersion { get; set; } = true;

    /// <summary>
    /// 帮助选项名称
    /// </summary>
    public string[] HelpOptions { get; set; } = ["help", "h"];

    /// <summary>
    /// 版本选项名称
    /// </summary>
    public string[] VersionOptions { get; set; } = ["version", "v"];

    /// <summary>
    /// 值分隔符
    /// </summary>
    public char[] ValueSeparators { get; set; } = ['=', ':'];

    /// <summary>
    /// 停止解析标记（通常是 --）
    /// </summary>
    public string StopParsingMarker { get; set; } = "--";
}

/// <summary>
/// 参数解析异常
/// </summary>
public class ArgumentParseException : Exception
{
    /// <summary>
    /// 创建参数解析异常
    /// </summary>
    /// <param name="message">错误消息</param>
    public ArgumentParseException(string message) : base(message)
    {
    }

    /// <summary>
    /// 创建参数解析异常
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="argumentName">参数名称</param>
    public ArgumentParseException(string message, string argumentName) : base(message)
    {
        ArgumentName = argumentName;
    }

    /// <summary>
    /// 创建参数解析异常
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public ArgumentParseException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string? ArgumentName { get; }
}

/// <summary>
/// 命令行参数token
/// </summary>
public class ArgumentToken
{
    /// <summary>
    /// 创建参数token
    /// </summary>
    /// <param name="type">token类型</param>
    /// <param name="value">token值</param>
    /// <param name="position">位置</param>
    public ArgumentToken(TokenType type, string value, int position)
    {
        Type = type;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Position = position;
    }

    /// <summary>
    /// Token类型
    /// </summary>
    public TokenType Type { get; }

    /// <summary>
    /// Token值
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 原始参数位置
    /// </summary>
    public int Position { get; }
}

/// <summary>
/// Token类型
/// </summary>
public enum TokenType
{
    /// <summary>
    /// 长选项 (--option)
    /// </summary>
    LongOption,

    /// <summary>
    /// 短选项 (-o)
    /// </summary>
    ShortOption,

    /// <summary>
    /// 组合短选项 (-abc)
    /// </summary>
    CombinedShortOptions,

    /// <summary>
    /// 键值对 (key=value)
    /// </summary>
    KeyValue,

    /// <summary>
    /// 普通值
    /// </summary>
    Value,

    /// <summary>
    /// 停止解析标记 (--)
    /// </summary>
    StopParsing,

    /// <summary>
    /// 命令
    /// </summary>
    Command
}
