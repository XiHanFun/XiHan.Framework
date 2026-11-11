// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// Web 宿主上的启动 Banner
/// </summary>
/// <remarks>
/// <c>AddXiHan</c> 登记带入口程序集版本的选项；宿主启动时先于其他宿主服务打印，按默认区域翻译；配置键关掉后不打印
/// </remarks>
public sealed class StartupBannerTests
{
    [Fact]
    public async Task AddXiHan_RegistersOptionsFromConfiguration_WithTheEntryAssemblyVersion()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddXiHan(static _ => { });
        await using var app = builder.Build();

        var options = app.Services.GetRequiredService<StartupBannerOptions>();
        Assert.True(options.Enabled);
        Assert.Null(options.Output);
        // 版本取自入口程序集的 InformationalVersion，去掉 + 之后的提交哈希
        var informational = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Assert.NotNull(informational);
        Assert.Equal(informational.Split('+')[0], options.ApplicationVersion);

        var disabled = WebApplication.CreateBuilder();
        disabled.Configuration["XiHan:Kernel:Banner:Enabled"] = "false";
        disabled.AddXiHan(static _ => { });
        await using var disabledApp = disabled.Build();
        Assert.False(disabledApp.Services.GetRequiredService<StartupBannerOptions>().Enabled);
    }

    [Fact]
    public async Task StartAsync_PrintsTheBannerBeforeOtherHostedServices_InTheDefaultLocale()
    {
        var output = new StringWriter();
        await using var app = await StartAsync(output, enabled: null);

        var text = output.ToString();
        Assert.StartsWith(Environment.NewLine + "   _  __ ______  _____    _   __" + Environment.NewLine, text, StringComparison.Ordinal);
        Assert.Contains($"XiHan Framework v{FrameworkInfo.Version}{Environment.NewLine}快速、轻量、高效、用心的 .NET 框架：微内核、能力图谱与管道执行。", text, StringComparison.Ordinal);
        Assert.Contains($"应用：{app.Environment.ApplicationName} v1.2.3", text, StringComparison.Ordinal);
        Assert.Contains($"环境：{app.Environment.EnvironmentName}", text, StringComparison.Ordinal);
        Assert.Contains("运行时：.NET ", text, StringComparison.Ordinal);
        Assert.Contains("操作系统：", text, StringComparison.Ordinal);
        // 应用自己的写入器不是控制台，不写颜色序列
        Assert.DoesNotContain("\u001b", text, StringComparison.Ordinal);
        // 应用的宿主服务登记在 AddXiHan 之前，Banner 仍在它启动之前打印
        Assert.EndsWith("hosted-service-started" + Environment.NewLine, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WhenDisabledByConfiguration_PrintsNothing()
    {
        var output = new StringWriter();
        await using var app = await StartAsync(output, enabled: "false");

        Assert.Equal("hosted-service-started" + Environment.NewLine, output.ToString());
    }

    /// <summary>
    /// 启动一个 Banner 写到 <paramref name="output"/>、按 zh-CN 翻译的 Web 应用
    /// </summary>
    /// <remarks>
    /// 应用先登记自己的选项（改写入目标，仍遵从配置开关）；解析器直接登记进容器，不启动本地化特性，避免改动进程默认作用域影响并行的用例
    /// </remarks>
    /// <param name="output">Banner 与标记服务共用的写入器</param>
    /// <param name="enabled"><c>XiHan:Kernel:Banner:Enabled</c> 的值；为 <see langword="null"/> 时不配置</param>
    private static async Task<WebApplication> StartAsync(StringWriter output, string? enabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (enabled is not null)
        {
            builder.Configuration["XiHan:Kernel:Banner:Enabled"] = enabled;
        }

        builder.Services.AddSingleton(StartupBannerOptions.FromConfiguration(builder.Configuration, new StartupBannerOptions { Output = output, ApplicationVersion = "1.2.3" }));
        builder.Services.AddSingleton<IMessageResolver>(new InMemoryMessageLocalizer([MessageCatalog.FromEmbeddedResources("kernel", typeof(App))], new LocalizationOptions { DefaultLocale = "zh-CN" }));
        builder.Services.AddHostedService(_ => new MarkerHostedService(output, "hosted-service-started"));
        builder.AddXiHan(static _ => { });
        var app = builder.Build();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
