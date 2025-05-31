#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DebugOptions
// Guid:d9e6f3a1-4b2c-5d8e-9f1a-2b3c4d5e6f7g
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/2 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Debugging;

/// <summary>
/// 脚本调试选项
/// </summary>
public class DebugOptions
{
    /// <summary>
    /// 是否启用调试
    /// </summary>
    public bool EnableDebugging { get; set; } = false;

    /// <summary>
    /// 是否生成完整的调试信息
    /// </summary>
    public bool GenerateFullDebugInfo { get; set; } = true;

    /// <summary>
    /// 是否保留原始代码映射
    /// </summary>
    public bool PreserveCodeMapping { get; set; } = true;

    /// <summary>
    /// 断点列表
    /// </summary>
    public List<Breakpoint> Breakpoints { get; set; } = [];

    /// <summary>
    /// 调试输出级别
    /// </summary>
    public DebugLevel DebugLevel { get; set; } = DebugLevel.Information;

    /// <summary>
    /// 是否启用变量监视
    /// </summary>
    public bool EnableVariableWatch { get; set; } = true;

    /// <summary>
    /// 最大调试输出长度
    /// </summary>
    public int MaxDebugOutputLength { get; set; } = 10000;

    /// <summary>
    /// 调试会话超时时间（毫秒）
    /// </summary>
    public int DebugSessionTimeoutMs { get; set; } = 300000; // 5分钟

    /// <summary>
    /// 是否启用性能分析
    /// </summary>
    public bool EnableProfiling { get; set; } = false;

    /// <summary>
    /// 创建默认调试配置
    /// </summary>
    public static DebugOptions Default => new();

    /// <summary>
    /// 创建详细调试配置
    /// </summary>
    public static DebugOptions Verbose()
    {
        return new DebugOptions
        {
            EnableDebugging = true,
            GenerateFullDebugInfo = true,
            PreserveCodeMapping = true,
            DebugLevel = DebugLevel.Verbose,
            EnableVariableWatch = true,
            EnableProfiling = true
        };
    }

    /// <summary>
    /// 创建生产环境调试配置
    /// </summary>
    public static DebugOptions Production()
    {
        return new DebugOptions
        {
            EnableDebugging = false,
            GenerateFullDebugInfo = false,
            PreserveCodeMapping = false,
            DebugLevel = DebugLevel.Error,
            EnableVariableWatch = false,
            EnableProfiling = false
        };
    }
}

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

/// <summary>
/// 调试级别
/// </summary>
public enum DebugLevel
{
    /// <summary>
    /// 无输出
    /// </summary>
    None,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 信息
    /// </summary>
    Information,

    /// <summary>
    /// 详细
    /// </summary>
    Verbose
}

/// <summary>
/// 命中条件
/// </summary>
public enum HitCountCondition
{
    /// <summary>
    /// 总是命中
    /// </summary>
    Always,

    /// <summary>
    /// 等于指定次数
    /// </summary>
    Equal,

    /// <summary>
    /// 大于等于指定次数
    /// </summary>
    GreaterOrEqual,

    /// <summary>
    /// 是指定次数的倍数
    /// </summary>
    Multiple
} 
