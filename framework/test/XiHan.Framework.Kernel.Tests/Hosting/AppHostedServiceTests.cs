// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 应用在通用宿主生命周期里的位置
/// </summary>
/// <remarks>
/// 应用在宿主的 <c>StartingAsync</c> 阶段执行启动核对并启动特性，在 <c>StoppedAsync</c> 阶段停止特性：应用与特性登记的普通宿主服务（含 <see cref="BackgroundService"/>）
/// 不论登记在 <c>AddXiHan</c> 之前还是由特性登记、宿主是否并发启停，都在特性就绪之后才开始、在特性停止之前就已停止；启动核对不过时它们一个都没有开始
/// </remarks>
public sealed class AppHostedServiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HostedServices_StartAfterFeaturesAreReady_AndStopBeforeFeaturesStop(bool registeredByFeature, bool concurrent)
    {
        var log = new List<string>();
        var background = new RecordingBackgroundService(log);
        var feature = new RecordingFeature(Manifests.Create("xihan.test.feature"), log)
        {
            OnConfigure = ctx =>
            {
                if (registeredByFeature)
                {
                    ctx.Services.AddSingleton<IHostedService>(background);
                }
            },
        };
        var builder = Builder(concurrent);
        if (!registeredByFeature)
        {
            // 登记在 AddXiHan 之前：按登记顺序它排在内核的宿主服务前面
            builder.Services.AddSingleton<IHostedService>(background);
        }

        builder.AddXiHan(b => b.UseFeature(feature));
        using var host = builder.Build();
        var app = host.Services.GetRequiredService<App>();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await background.Executing.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(LifecycleState.Ready, app.State);
        await host.StopAsync(TestContext.Current.CancellationToken);

        string[] expected = ["configure:xihan.test.feature", "start:xihan.test.feature", "background:start", "background:stop", "stop:xihan.test.feature"];
        Assert.Equal(expected, log);
        Assert.Equal(LifecycleState.Stopped, app.State);
    }

    [Fact]
    public async Task FailedStartupCheck_StartsNoHostedService()
    {
        var log = new List<string>();
        var background = new RecordingBackgroundService(log);
        var feature = new RecordingFeature(Manifests.Create("xihan.test.feature"), log)
        {
            OnConfigure = static ctx => ctx.AddStartupCheck(static _ => throw new FrameworkException("TESTS_VALIDATION_001", "A contract references something that does not exist.")),
        };
        var builder = Builder(concurrent: false);
        builder.Services.AddSingleton<IHostedService>(background);
        builder.AddXiHan(b => b.UseFeature(feature));
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        // 核对在任何宿主服务的 StartAsync 之前失败：后台服务没有开始，宿主释放时也没有要取消的执行体
        Assert.Equal("TESTS_VALIDATION_001", exception.Code);
        string[] expected = ["configure:xihan.test.feature"];
        Assert.Equal(expected, log);
        Assert.False(background.Executing.Task.IsCompleted);
    }

    /// <summary>
    /// 创建关掉 Banner 的空宿主构建器
    /// </summary>
    /// <param name="concurrent">宿主是否并发启动与停止宿主服务</param>
    private static HostApplicationBuilder Builder(bool concurrent)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration["XiHan:Kernel:Banner:Enabled"] = "false";
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = concurrent;
            options.ServicesStopConcurrently = concurrent;
        });
        return builder;
    }
}
