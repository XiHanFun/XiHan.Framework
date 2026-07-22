// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
