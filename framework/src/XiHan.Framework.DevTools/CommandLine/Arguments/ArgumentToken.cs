// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DevTools.CommandLine.Arguments;

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
