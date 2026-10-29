// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Plugins.OutOfProcess;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 以 Go 写的远程缓存插件 CacheGoPlugin为插件目录启动测试宿主的辅助。
/// </summary>
/// <remarks>
/// framework-sdk-go 的 examples/cache。测试运行时用本机的 go 构建它，没有 go 就跳过。
/// </remarks>
internal static class CacheGoPluginHost
{
    /// <summary>
    /// 示例插件的清单 ID。
    /// </summary>
    public const string PluginId = "xihan.samples.cache-go";

    /// <summary>
    /// 示例源码目录 samples/CacheGoPlugin。
    /// </summary>
    public static string SourceDirectory { get; } = Path.Combine(LocateRoot(), "samples", "CacheGoPlugin");

    /// <summary>
    /// 构建产物目录：可执行文件与清单。
    /// </summary>
    public static string Directory { get; } = Path.Combine(SourceDirectory, "bin");

    /// <summary>
    /// 只构建一次。
    /// </summary>
    /// <remarks>
    /// 值为空表示成功，否则是跳过原因。
    /// </remarks>
    private static readonly Lazy<string?> _build = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 构建结果：空表示已构建，否则是跳过原因。
    /// </summary>
    public static string? BuildReason => _build.Value;

    /// <summary>
    /// 确保示例已构建。
    /// </summary>
    /// <remarks>
    /// 本机没有 go 或构建失败时跳过当前测试。
    /// </remarks>
    public static void EnsureBuiltOrSkip()
    {
        if (BuildReason is { } reason)
        {
            Assert.Skip(reason);
        }
    }

    /// <summary>
    /// 宿主配置：插件目录、较短的健康轮询、插件的 Cache 配置节与适配器超时。
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Configuration(params (string Key, string Value)[] extra)
    {
        var values = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:OutOfProcess:Directories:0", Directory),
            new("XiHan:Plugins:OutOfProcess:HealthIntervalSeconds", "1"),
            new("XiHan:Plugins:OutOfProcess:StopGraceSeconds", "5"),
            new("XiHan:Cache:MaxEntries", "100000"),
            new("XiHan:Caching:Remote:TimeoutMilliseconds", "5000"),
        };
        values.AddRange(extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)));
        return values;
    }

    /// <summary>
    /// 构建示例后启动装配进程外插件特性与远程缓存特性的宿主。
    /// </summary>
    public static Task<TestHost> StartAsync(Action<AppBuilder>? configure = null, params (string Key, string Value)[] extra)
    {
        EnsureBuiltOrSkip();
        return TestHost.StartAsync(
            b =>
            {
                b.UseFeature<OutOfProcessPluginsFeature>();
                b.UseFeature<RemoteCachingFeature>();
                configure?.Invoke(b);
            },
            Configuration(extra));
    }

    /// <summary>
    /// 用本机的 go 构建 cache-go 并把清单复制进产物目录。
    /// </summary>
    private static string? Build()
    {
        var executable = Path.Combine(Directory, OperatingSystem.IsWindows() ? "cache-go.exe" : "cache-go");
        var info = new ProcessStartInfo("go", ["build", "-o", executable, "."])
        {
            WorkingDirectory = SourceDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return "go could not be started.";
            }

            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return "go build failed: " + error;
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return "go is not on PATH; the Go cache plugin sample is skipped.";
        }

        File.Copy(Path.Combine(SourceDirectory, "xihan.manifest.json"), Path.Combine(Directory, "xihan.manifest.json"), overwrite: true);
        return null;
    }

    /// <summary>
    /// 向上找到含解决方案文件的目录。
    /// </summary>
    private static string LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The solution root was not found.");
    }
}
