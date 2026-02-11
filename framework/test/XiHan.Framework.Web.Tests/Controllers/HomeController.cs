#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HomeController
// Guid:a21f5c4e-1501-4062-af84-28ac33dfb64c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/04 14:42:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.IO;
using XiHan.Framework.Utils.Reflections;
using XiHan.Framework.Utils.Runtime;
using XiHan.Framework.Utils.Security.ErrorObfuscation;

namespace XiHan.Framework.Web.Tests.Controllers;

/// <summary>
/// HomeController
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
[Produces("application/json")]
[Tags("用户模块")]
[ApiExplorerSettings(GroupName = "v1")]
public class HomeController : ControllerBase
{
    /// <summary>
    /// SystemInfo
    /// </summary>
    /// <returns></returns>
    [HttpPost("SystemInfo")]
    public IActionResult SystemInfo()
    {
        var systemInfo = SystemInfoManager.GetSystemInfo();
        return Ok(systemInfo);
    }

    /// <summary>
    /// NuGetPackagesInfo
    /// </summary>
    /// <returns></returns>
    [HttpPost("NuGetPackagesInfo")]
    public IActionResult NuGetPackagesInfo()
    {
        var nuGetPackages = ReflectionHelper.GetNuGetPackages("XiHan.Framework");
        return Ok(nuGetPackages);
    }

    /// <summary>
    /// RuntimeMonitor
    /// </summary>
    /// <returns></returns>
    [HttpPost("RuntimeMonitor")]
    public async Task<IActionResult> RuntimeMonitor()
    {
        // 创建监控器
        using var monitor = new RuntimeMonitor(TimeSpan.FromSeconds(5), 500);

        // 开始监控
        monitor.StartMonitoring();

        // 获取当前性能快照
        var snapshot = monitor.GetCurrentSnapshot();
        Console.WriteLine($"CPU使用率: {snapshot.CpuUsage:F1}%");
        Console.WriteLine($"内存使用: {snapshot.MemoryUsage.FormatFileSizeToString()}");

        // 等待一段时间收集数据
        await Task.Delay(TimeSpan.FromMinutes(1));

        // 分析趋势
        var cpuTrend = monitor.AnalyzeTrend(PerformanceCounterType.CpuUsage, TimeSpan.FromMinutes(5));

        monitor.StopMonitoring();

        Console.WriteLine($"CPU平均使用率: {cpuTrend.AverageValue:F1}%");
        Console.WriteLine($"CPU使用趋势: {(cpuTrend.Trend > 0 ? "上升" : "下降")}");
        return Ok(new
        {
            snapshot.CpuUsage,
            MemoryUsage = snapshot.MemoryUsage.FormatFileSizeToString(),
            CpuTrend = new
            {
                cpuTrend.AverageValue,
                Trend = cpuTrend.Trend > 0 ? "上升" : "下降"
            }
        });
    }

    /// <summary>
    /// ErrorObfuscation
    /// </summary>
    /// <returns></returns>
    [HttpPost("ErrorObfuscation")]
    public IActionResult ErrorObfuscation()
    {
        var error = ErrorObfuscationHelper.GenerateObfuscatedError();
        return Ok(error);
    }
}
