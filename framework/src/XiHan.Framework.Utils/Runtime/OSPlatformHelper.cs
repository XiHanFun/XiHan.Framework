#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OSPlatformHelper
// Guid:d404f006-9a93-45b2-b33b-8ec201355621
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-04-09 上午 06:49:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XiHan.Framework.Utils.Runtime;

/// <summary>
/// 系统运行时信息
/// </summary>
public class RuntimeInfo
{
    /// <summary>
    /// 操作系统平台类型
    /// </summary>
    public OSPlatformType PlatformType { get; set; }

    /// <summary>
    /// 操作系统名称
    /// </summary>
    public string OSName { get; set; } = string.Empty;

    /// <summary>
    /// 操作系统描述
    /// </summary>
    public string OSDescription { get; set; } = string.Empty;

    /// <summary>
    /// 操作系统版本
    /// </summary>
    public string OSVersion { get; set; } = string.Empty;

    /// <summary>
    /// 操作系统架构
    /// </summary>
    public string OSArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// 进程架构
    /// </summary>
    public string ProcessArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// 运行时框架描述
    /// </summary>
    public string FrameworkDescription { get; set; } = string.Empty;

    /// <summary>
    /// 运行时版本
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>
    /// 是否64位操作系统
    /// </summary>
    public bool Is64BitOperatingSystem { get; set; }

    /// <summary>
    /// 是否64位进程
    /// </summary>
    public bool Is64BitProcess { get; set; }

    /// <summary>
    /// 是否Unix系统
    /// </summary>
    public bool IsUnixSystem { get; set; }

    /// <summary>
    /// 是否交互模式
    /// </summary>
    public bool IsInteractive { get; set; }

    /// <summary>
    /// 处理器数量
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// 系统目录
    /// </summary>
    public string SystemDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 当前目录
    /// </summary>
    public string CurrentDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 机器名称
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 用户域名
    /// </summary>
    public string UserDomainName { get; set; } = string.Empty;

    /// <summary>
    /// 工作集大小（字节）
    /// </summary>
    public long WorkingSet { get; set; }

    /// <summary>
    /// 系统启动时间
    /// </summary>
    public DateTime SystemStartTime { get; set; }

    /// <summary>
    /// 系统运行时间
    /// </summary>
    public TimeSpan SystemUptime { get; set; }

    /// <summary>
    /// 进程启动时间
    /// </summary>
    public DateTime ProcessStartTime { get; set; }

    /// <summary>
    /// 进程运行时间
    /// </summary>
    public TimeSpan ProcessUptime { get; set; }

    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 进程名称
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// CLR版本
    /// </summary>
    public string ClrVersion { get; set; } = string.Empty;

    /// <summary>
    /// 环境变量数量
    /// </summary>
    public int EnvironmentVariableCount { get; set; }

    /// <summary>
    /// 命令行参数
    /// </summary>
    public string[] CommandLineArgs { get; set; } = [];

    /// <summary>
    /// 信息收集时间
    /// </summary>
    public DateTime CollectedAt { get; set; }
}

/// <summary>
/// 操作系统平台类型枚举
/// </summary>
public enum OSPlatformType
{
    /// <summary>
    /// 未知平台
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Windows 平台
    /// </summary>
    Windows = 1,

    /// <summary>
    /// Linux 平台
    /// </summary>
    Linux = 2,

    /// <summary>
    /// macOS 平台
    /// </summary>
    MacOS = 3,

    /// <summary>
    /// FreeBSD 平台
    /// </summary>
    FreeBSD = 4
}

/// <summary>
/// 运行时框架类型枚举
/// </summary>
public enum RuntimeFrameworkType
{
    /// <summary>
    /// 未知框架
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// .NET Framework
    /// </summary>
    NetFramework = 1,

    /// <summary>
    /// .NET Core
    /// </summary>
    NetCore = 2,

    /// <summary>
    /// .NET 5+
    /// </summary>
    Net5Plus = 3,

    /// <summary>
    /// Mono
    /// </summary>
    Mono = 4
}

/// <summary>
/// 操作系统平台帮助类
/// </summary>
public static class OSPlatformHelper
{
    private static readonly Lazy<RuntimeInfo> _runtimeInfo = new(CollectRuntimeInfo);
    private static readonly Lock _lockObject = new();

    /// <summary>
    /// 操作系统平台类型
    /// </summary>
    public static OSPlatformType PlatformType => GetRuntimeInfo().PlatformType;

    /// <summary>
    /// 操作系统名称
    /// </summary>
    public static string OperatingSystem => GetRuntimeInfo().OSName;

    /// <summary>
    /// 是否 Unix 系统
    /// </summary>
    public static bool OsIsUnix => GetRuntimeInfo().IsUnixSystem;

    /// <summary>
    /// 系统描述
    /// </summary>
    public static string OsDescription => GetRuntimeInfo().OSDescription;

    /// <summary>
    /// 系统版本
    /// </summary>
    public static string OsVersion => GetRuntimeInfo().OSVersion;

    /// <summary>
    /// 系统平台
    /// </summary>
    public static string Platform => GetRuntimeInfo().OSName;

    /// <summary>
    /// 系统架构
    /// </summary>
    public static string OsArchitecture => GetRuntimeInfo().OSArchitecture;

    /// <summary>
    /// 系统目录
    /// </summary>
    public static string SystemDirectory => GetRuntimeInfo().SystemDirectory;

    /// <summary>
    /// 交互模式描述
    /// </summary>
    public static string InteractiveMode => GetRuntimeInfo().IsInteractive ? "交互运行" : "非交互运行";

    /// <summary>
    /// 是否 Windows 平台
    /// </summary>
    public static bool IsWindows => PlatformType == OSPlatformType.Windows;

    /// <summary>
    /// 是否 Linux 平台
    /// </summary>
    public static bool IsLinux => PlatformType == OSPlatformType.Linux;

    /// <summary>
    /// 是否 macOS 平台
    /// </summary>
    public static bool IsMacOS => PlatformType == OSPlatformType.MacOS;

    /// <summary>
    /// 是否 FreeBSD 平台
    /// </summary>
    public static bool IsFreeBSD => PlatformType == OSPlatformType.FreeBSD;

    /// <summary>
    /// 是否64位操作系统
    /// </summary>
    public static bool Is64BitOperatingSystem => GetRuntimeInfo().Is64BitOperatingSystem;

    /// <summary>
    /// 是否64位进程
    /// </summary>
    public static bool Is64BitProcess => GetRuntimeInfo().Is64BitProcess;

    /// <summary>
    /// 运行时框架描述
    /// </summary>
    public static string FrameworkDescription => GetRuntimeInfo().FrameworkDescription;

    /// <summary>
    /// 处理器数量
    /// </summary>
    public static int ProcessorCount => GetRuntimeInfo().ProcessorCount;

    /// <summary>
    /// 机器名称
    /// </summary>
    public static string MachineName => GetRuntimeInfo().MachineName;

    /// <summary>
    /// 当前用户名
    /// </summary>
    public static string UserName => GetRuntimeInfo().UserName;

    /// <summary>
    /// 系统运行时间
    /// </summary>
    public static TimeSpan SystemUptime => Environment.TickCount64 > 0
        ? TimeSpan.FromMilliseconds(Environment.TickCount64)
        : GetRuntimeInfo().SystemUptime;

    /// <summary>
    /// 进程运行时间
    /// </summary>
    public static TimeSpan ProcessUptime => DateTime.Now - GetRuntimeInfo().ProcessStartTime;

    /// <summary>
    /// 获取运行时信息
    /// </summary>
    public static RuntimeInfo GetRuntimeInfo() => _runtimeInfo.Value;

    /// <summary>
    /// 重新收集运行时信息（刷新缓存）
    /// </summary>
    public static RuntimeInfo RefreshRuntimeInfo()
    {
        lock (_lockObject)
        {
            // 由于使用了Lazy<T>，需要重新创建实例来强制刷新
            var newRuntimeInfo = CollectRuntimeInfo();
            return newRuntimeInfo;
        }
    }

    /// <summary>
    /// 获取环境变量
    /// </summary>
    /// <param name="variableName">变量名</param>
    /// <param name="target">环境变量目标</param>
    /// <returns>环境变量值</returns>
    public static string? GetEnvironmentVariable(string variableName, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        return Environment.GetEnvironmentVariable(variableName, target);
    }

    /// <summary>
    /// 获取所有环境变量
    /// </summary>
    /// <param name="target">环境变量目标</param>
    /// <returns>环境变量字典</returns>
    public static Dictionary<string, string?> GetAllEnvironmentVariables(EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        var variables = Environment.GetEnvironmentVariables(target);
        return variables.Cast<DictionaryEntry>()
            .ToDictionary(entry => entry.Key.ToString()!, entry => entry.Value?.ToString());
    }

    /// <summary>
    /// 获取系统性能计数信息摘要
    /// </summary>
    /// <returns>性能摘要字符串</returns>
    public static string GetPerformanceSummary()
    {
        var info = GetRuntimeInfo();
        return $"OS: {info.OSName} {info.OSVersion} ({info.OSArchitecture}), " +
               $"Framework: {info.FrameworkDescription}, " +
               $"Process: {info.ProcessName} (PID: {info.ProcessId}), " +
               $"Memory: {info.WorkingSet / 1024 / 1024} MB, " +
               $"Processors: {info.ProcessorCount}, " +
               $"Uptime: {info.SystemUptime:dd\\.hh\\:mm\\:ss}";
    }

    /// <summary>
    /// 检查特定的操作系统版本
    /// </summary>
    /// <param name="minimumVersion">最小版本要求</param>
    /// <returns>是否满足版本要求</returns>
    public static bool CheckOSVersion(Version minimumVersion)
    {
        try
        {
            return Environment.OSVersion.Version >= minimumVersion;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取.NET运行时位置
    /// </summary>
    /// <returns>运行时路径</returns>
    public static string GetRuntimeLocation()
    {
        try
        {
            return Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 收集运行时信息
    /// </summary>
    private static RuntimeInfo CollectRuntimeInfo()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var runtimeInfo = new RuntimeInfo
            {
                PlatformType = GetPlatformType(),
                OSName = GetOperatingSystemName(),
                OSDescription = RuntimeInformation.OSDescription,
                OSVersion = Environment.OSVersion.Version.ToString(),
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                RuntimeVersion = Environment.Version.ToString(),
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                IsUnixSystem = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                              RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                              RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD),
                IsInteractive = Environment.UserInteractive,
                ProcessorCount = Environment.ProcessorCount,
                SystemDirectory = Environment.SystemDirectory,
                CurrentDirectory = Environment.CurrentDirectory,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                UserDomainName = Environment.UserDomainName,
                WorkingSet = Environment.WorkingSet,
                SystemStartTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64),
                SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                ProcessStartTime = currentProcess.StartTime,
                ProcessUptime = DateTime.Now - currentProcess.StartTime,
                ProcessId = currentProcess.Id,
                ProcessName = currentProcess.ProcessName,
                ClrVersion = Environment.Version.ToString(),
                EnvironmentVariableCount = Environment.GetEnvironmentVariables().Count,
                CommandLineArgs = Environment.GetCommandLineArgs(),
                CollectedAt = DateTime.Now
            };

            return runtimeInfo;
        }
        catch (Exception ex)
        {
            // 异常时返回基本信息
            return new RuntimeInfo
            {
                PlatformType = OSPlatformType.Unknown,
                OSName = "Unknown",
                OSDescription = $"Error collecting info: {ex.Message}",
                CollectedAt = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 获取操作系统平台类型
    /// </summary>
    private static OSPlatformType GetPlatformType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatformType.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OSPlatformType.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatformType.MacOS;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) ? OSPlatformType.FreeBSD : OSPlatformType.Unknown;
    }

    /// <summary>
    /// 获取操作系统名称
    /// </summary>
    private static string GetOperatingSystemName()
    {
        var platformType = GetPlatformType();
        return platformType switch
        {
            OSPlatformType.Windows => "Windows",
            OSPlatformType.Linux => "Linux",
            OSPlatformType.MacOS => "macOS",
            OSPlatformType.FreeBSD => "FreeBSD",
            _ => "Unknown"
        };
    }
}
