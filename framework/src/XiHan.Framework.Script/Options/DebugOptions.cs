// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Debugging;
using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Options;

/// <summary>
/// 脚本调试选项
/// </summary>
public class DebugOptions
{
    /// <summary>
    /// 创建默认调试配置
    /// </summary>
    public static DebugOptions Default => new();

    /// <summary>
    /// 是否启用调试
    /// </summary>
    public bool EnableDebugging { get; set; }

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
    /// 调试会话超时时间(毫秒)
    /// </summary>
    public int DebugSessionTimeoutMs { get; set; } = 300000; // 5分钟

    /// <summary>
    /// 是否启用性能分析
    /// </summary>
    public bool EnableProfiling { get; set; }

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
