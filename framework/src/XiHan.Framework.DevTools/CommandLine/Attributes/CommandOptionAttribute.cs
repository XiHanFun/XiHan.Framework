#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandOptionAttribute
// Guid:f5b3d147-8c4e-4d29-9a2f-12fd84c18139
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
