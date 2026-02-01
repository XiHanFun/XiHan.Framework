#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Breakpoint
// Guid:11490851-4184-4b72-8ede-6dea571f81a0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/01 11:03:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Debugging;

/// <summary>
/// 断点信息
/// </summary>
public class Breakpoint
{
    /// <summary>
    /// 行号
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// 列号
    /// </summary>
    public int? ColumnNumber { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 条件表达式
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 命中次数
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// 命中条件
    /// </summary>
    public HitCountCondition HitCountCondition { get; set; } = HitCountCondition.Always;

    /// <summary>
    /// 命中次数阈值
    /// </summary>
    public int HitCountThreshold { get; set; } = 1;
}
