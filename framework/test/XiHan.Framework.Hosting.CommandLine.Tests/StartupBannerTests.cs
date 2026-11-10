// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Minimal;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 命令行宿主上的启动 Banner 选项
/// </summary>
/// <remarks>
/// <c>AddXiHanCommandLine</c> 把 Banner 换成缺省关闭、写 stderr，与 <c>AddXiHan</c> 的先后无关；没有它的通用宿主照常打印。实际执行命令时 stdout 的内容见 <see cref="StartupBannerConsoleTests"/>
/// </remarks>
public sealed class StartupBannerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddXiHanCommandLine_DisablesTheBanner_AndPointsItAtStandardError(bool commandLineFirst)
    {
        var builder = Host.CreateApplicationBuilder();
        if (commandLineFirst)
        {
            builder.Services.AddXiHanCommandLine();
        }

        builder.AddXiHan(static xihan => xihan.UseMinimalProfile());
        if (!commandLineFirst)
        {
            builder.Services.AddXiHanCommandLine();
        }

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<StartupBannerOptions>();

        Assert.False(options.Enabled);
        Assert.Same(Console.Error, options.Output);
        Assert.False(string.IsNullOrEmpty(options.ApplicationVersion));
    }

    [Fact]
    public void AddXiHan_OnTheGenericHost_RegistersAnEnabledBannerWithTheEntryAssemblyVersion()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile());

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<StartupBannerOptions>();

        // 没有 AddXiHanCommandLine 的通用宿主（Worker）照常打印到 stdout
        Assert.True(options.Enabled);
        Assert.Null(options.Output);
        Assert.False(string.IsNullOrEmpty(options.ApplicationVersion));
    }
}
