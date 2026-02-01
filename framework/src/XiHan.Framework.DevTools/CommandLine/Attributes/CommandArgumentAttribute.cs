#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandArgumentAttribute
// Guid:d5a5a6b2-bea2-4e96-8e89-4d4a6d9269b7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:01:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 命令行参数属性标记（位置参数）
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CommandArgumentAttribute : Attribute
{
    /// <summary>
    /// 创建参数属性
    /// </summary>
    /// <param name="position">参数位置</param>
    /// <param name="name">参数名称</param>
    public CommandArgumentAttribute(int position, string name)
    {
        Position = position;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 参数位置（从0开始）
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 是否支持多值（通常用于最后一个参数）
    /// </summary>
    public bool AllowMultiple { get; set; }
}
