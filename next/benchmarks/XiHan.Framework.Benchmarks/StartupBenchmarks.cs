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
    private static readonly IConfiguration Configuration = new ConfigurationBuilder().Build();

    [Params(1, 10)]
    public int Features { get; set; }

    [Benchmark]
    public async Task BuildAndStart()
    {
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, Configuration);
        for (var i = 0; i < Features; i++)
        {
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
