// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 命令行选项属性标记
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CommandOptionAttribute : Attribute
{
    /// <summary>
    /// 创建选项属性
    /// </summary>
    /// <param name="longName">长选项名称</param>
    public CommandOptionAttribute(string longName)
    {
        LongName = longName ?? throw new ArgumentNullException(nameof(longName));
    }

    /// <summary>
    /// 创建选项属性
    /// </summary>
    /// <param name="longName">长选项名称</param>
    /// <param name="shortName">短选项名称</param>
    public CommandOptionAttribute(string longName, string shortName) : this(longName)
    {
        ShortName = shortName;
    }

    /// <summary>
    /// 长选项名称
    /// </summary>
    public string LongName { get; }

    /// <summary>
    /// 短选项名称
    /// </summary>
    public string? ShortName { get; set; }

    /// <summary>
    /// 选项描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 是否为布尔开关
    /// </summary>
    public bool IsSwitch { get; set; }

    /// <summary>
    /// 是否支持多值
    /// </summary>
    public bool AllowMultiple { get; set; }

    /// <summary>
    /// 值分隔符（用于多值选项）
    /// </summary>
    public char Separator { get; set; } = ',';

    /// <summary>
    /// 参数名称（用于帮助显示）
    /// </summary>
    public string? MetaName { get; set; }
}
