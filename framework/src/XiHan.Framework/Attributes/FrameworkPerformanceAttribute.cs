#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkPerformanceAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架性能特性
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FrameworkPerformanceAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="thresholdMs">性能阈值（毫秒）</param>
    /// <param name="logPerformance">是否记录性能数据</param>
    public FrameworkPerformanceAttribute(long thresholdMs = 1000, bool logPerformance = true)
    {
        ThresholdMs = thresholdMs;
        LogPerformance = logPerformance;
    }

    /// <summary>
    /// 性能阈值（毫秒）
    /// </summary>
    public long ThresholdMs { get; }

    /// <summary>
    /// 是否记录性能数据
    /// </summary>
    public bool LogPerformance { get; }
}
