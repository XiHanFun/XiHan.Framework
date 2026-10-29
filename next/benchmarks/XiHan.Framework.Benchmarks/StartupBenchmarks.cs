// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Benchmarks;

/// <summary>
/// 装配与启动开销：目标在宿主基线之上 &lt; 50 ms。
/// </summary>
[MemoryDiagnoser]
public class StartupBenchmarks
{
    /// <summary>
    /// 空配置。
    /// </summary>
    /// <remarks>
    /// 所有迭代共用，不计配置构建时间。
    /// </remarks>
    private static readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    /// <summary>
    /// 装配的特性数量。
    /// </summary>
    [Params(1, 10)]
    public int Features { get; set; }

    /// <summary>
    /// 装配 <see cref="Features"/> 个链式依赖的特性桩，构建容器，启动再停止应用。
    /// </summary>
    [Benchmark]
    public async Task BuildAndStart()
    {
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, _configuration);
        for (var i = 0; i < Features; i++)
        {
            // 每个特性依赖前一个特性提供的能力，形成一条链，让能力解析与拓扑排序的开销随数量线性增长
            var id = $"xihan.benchmark.feature{i}";
            var requires = i == 0 ? ["kernel.hosting@1"] : new[] { $"benchmark.feature{i - 1}@1" };
            builder.UseFeature(new StubFeature(TestManifests.Create(id, provides: [$"benchmark.feature{i}@1"], requires: requires)));
        }

        builder.Build();
        await using var provider = services.BuildServiceProvider();
        var app = provider.GetRequiredService<App>();
        await app.StartAsync(CancellationToken.None);
        await app.StopAsync(CancellationToken.None);
    }
}
