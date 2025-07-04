﻿using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Utils.HardwareInfos;
using XiHan.Framework.Utils.IO;
using XiHan.Framework.Utils.Reflections;
using XiHan.Framework.Utils.Runtime;

namespace XiHan.Framework.Web.Test.Controllers;

/// <summary>
/// HomeController
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
public class HomeController : ControllerBase
{
    /// <summary>
    /// HardwareInfo
    /// </summary>
    /// <returns></returns>
    [HttpPost("HardwareInfo")]
    public IActionResult HardwareInfo()
    {
        var hardwareInfo = HardwareInfoManager.GetSystemHardwareInfo();
        return Ok(hardwareInfo);
    }

    /// <summary>
    /// RuntimeInfo
    /// </summary>
    /// <returns></returns>
    [HttpPost("RuntimeInfo")]
    public IActionResult RuntimeInfo()
    {
        var systemRuntimeInfo = RuntimeInfoManger.GetSystemRuntimeInfo();
        return Ok(systemRuntimeInfo);
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
            CpuUsage = snapshot.CpuUsage,
            MemoryUsage = snapshot.MemoryUsage.FormatFileSizeToString(),
            CpuTrend = new
            {
                AverageValue = cpuTrend.AverageValue,
                Trend = cpuTrend.Trend > 0 ? "上升" : "下降"
            }
        });
    }
}
