// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
