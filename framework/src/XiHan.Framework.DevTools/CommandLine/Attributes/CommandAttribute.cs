#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandAttribute
// Guid:ad610f60-b65f-4b0d-8b1a-195b9fe22e70
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:02:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 命令属性标记
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CommandAttribute : Attribute
{
    /// <summary>
    /// 创建命令属性
    /// </summary>
    /// <param name="name">命令名称</param>
    public CommandAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 命令名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 命令别名
    /// </summary>
    public string[]? Aliases { get; set; }

    /// <summary>
    /// 命令描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否为默认命令
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 是否隐藏（不在帮助中显示）
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// 使用示例
    /// </summary>
    public string? Usage { get; set; }
}
