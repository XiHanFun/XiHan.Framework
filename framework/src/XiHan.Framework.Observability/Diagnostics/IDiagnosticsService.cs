#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDiagnosticsService
// Guid:e0f1a2b3-c4d5-46e7-f8a9-b0c1d2e3f4a5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 04:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Observability.Diagnostics;

/// <summary>
/// 诊断服务接口
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>
    /// 获取系统信息
    /// </summary>
    /// <returns>系统信息</returns>
    SystemInfo GetSystemInfo();

    /// <summary>
    /// 获取运行时信息
    /// </summary>
    /// <returns>运行时信息</returns>
    RuntimeInfo GetRuntimeInfo();

    /// <summary>
    /// 获取内存信息
    /// </summary>
    /// <returns>内存信息</returns>
    MemoryInfo GetMemoryInfo();

    /// <summary>
    /// 获取线程信息
    /// </summary>
    /// <returns>线程信息</returns>
    ThreadInfo GetThreadInfo();

    /// <summary>
    /// 执行垃圾回收
    /// </summary>
    void ForceGarbageCollection();

    /// <summary>
    /// 获取完整诊断报告
    /// </summary>
    /// <returns>诊断报告</returns>
    DiagnosticsReport GetDiagnosticsReport();
}
