// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Benchmarks;

/// <summary>
/// 空应用的常驻内存探针
/// </summary>
/// <remarks>
/// 开发计划第九节的两项预算要在一个干净的进程里测，BenchmarkDotNet 测不了：装配不带任何特性的应用并启动，记下冷启动耗时（JIT 下上限 100 ms；同一份代码按 Native AOT 发布成 <c>XiHan.Framework.StartupProbe</c>，目标 50 ms，ADR-0227），回收之后读常驻内存（Linux 上是 RSS，Windows 上是工作集），连同装配前运行时自己的常驻内存写成一行 JSON：<c>{"residentBytes":…,"runtimeBytes":…,"coldStartMilliseconds":…}</c>
/// </remarks>
internal static class ResidentMemoryProbe
{
    /// <summary>
    /// 运行探针，把结果写到 <paramref name="outputPath"/>
    /// </summary>
    public static async Task<int> RunAsync(string outputPath)
    {
        // 对照：同一进程里还没装配任何东西时的常驻内存，是运行时自己的那一份
        var runtime = ResidentBytes();

        // 冷启动：这个进程第一次装配、建容器、启动应用，含 JIT 与类型加载，是框架在宿主之上的额外开销
        var stopwatch = Stopwatch.StartNew();
        var services = new ServiceCollection();
        new AppBuilder(services, new ConfigurationBuilder().Build()).Build();
        await using var provider = services.BuildServiceProvider();
        var app = provider.GetRequiredService<App>();
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        var coldStart = stopwatch.Elapsed;

        var resident = ResidentBytes();
        await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await File.WriteAllTextAsync(outputPath, string.Create(CultureInfo.InvariantCulture, $"{{\"residentBytes\":{resident},\"runtimeBytes\":{runtime},\"coldStartMilliseconds\":{coldStart.TotalMilliseconds:F3}}}")).ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// 回收之后读进程的常驻内存
    /// </summary>
    private static long ResidentBytes()
    {
        // 两次完整回收：第一次让可终结的对象排队，第二次收掉终结之后的对象，读到的是稳态而不是峰值
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return process.WorkingSet64;
    }
}
