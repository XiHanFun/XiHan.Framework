// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 启动 Banner 的选项、登记与输出
/// </summary>
/// <remarks>
/// 版式与 4.x 一致：空行、Logo、框架名与版本、标语、寄语，再是应用、环境、运行时与操作系统；只有写到没有重定向的控制台才上色
/// </remarks>
public sealed partial class StartupBannerTests
{
    /// <summary>
    /// 用例里的英文模板
    /// </summary>
    private static readonly Dictionary<string, string> _templates = new(StringComparer.Ordinal)
    {
        [KernelMessages.BannerTagline] = "Tagline.",
        [KernelMessages.BannerApplication] = "Application: {application}",
        [KernelMessages.BannerEnvironment] = "Environment: {environment}",
        [KernelMessages.BannerRuntime] = "Runtime: {runtime} ({architecture})",
        [KernelMessages.BannerOperatingSystem] = "OS: {os}",
    };

    [Fact]
    public void FromConfiguration_ReadsEnabled_AndKeepsTheRestFromDefaults()
    {
        Assert.True(StartupBannerOptions.FromConfiguration(Hosts.Configuration()).Enabled);
        Assert.False(StartupBannerOptions.FromConfiguration(Hosts.Configuration(("XiHan:Kernel:Banner:Enabled", "false"))).Enabled);

        var output = new StringWriter();
        var defaults = new StartupBannerOptions { Enabled = false, Output = output, ApplicationVersion = "1.2.3" };
        Assert.Same(defaults, StartupBannerOptions.FromConfiguration(Hosts.Configuration(), defaults));

        // 配置里显式写的值覆盖宿主给的缺省，其余属性沿用缺省
        var enabled = StartupBannerOptions.FromConfiguration(Hosts.Configuration(("XiHan:Kernel:Banner:Enabled", "True")), defaults);
        Assert.True(enabled.Enabled);
        Assert.Same(output, enabled.Output);
        Assert.Equal("1.2.3", enabled.ApplicationVersion);

        var invalid = Assert.Throws<ArgumentException>(() => StartupBannerOptions.FromConfiguration(Hosts.Configuration(("XiHan:Kernel:Banner:Enabled", "maybe"))));
        Assert.Contains("XiHan:Kernel:Banner:Enabled", invalid.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RegistersBannerBeforeTheApp_AndKeepsOptionsTheApplicationRegistered()
    {
        var services = new ServiceCollection();
        new AppBuilder(services, Hosts.Configuration(("XiHan:Kernel:Banner:Enabled", "false"))).Build();

        // 注册顺序就是启动顺序；Banner 另在 StartingAsync 打印，早于全部宿主服务的 StartAsync
        Type?[] expected = [typeof(StartupBannerService), typeof(AppHostedService)];
        Assert.Equal(expected, services.Where(static d => d.ServiceType == typeof(IHostedService)).Select(static d => d.ImplementationType).ToArray());
        Assert.False(Assert.IsType<StartupBannerOptions>(services.Single(static d => d.ServiceType == typeof(StartupBannerOptions)).ImplementationInstance).Enabled);

        var own = new StartupBannerOptions { ApplicationVersion = "9.9.9" };
        var withOwn = new ServiceCollection();
        withOwn.AddSingleton(own);
        new AppBuilder(withOwn, Hosts.Configuration(("XiHan:Kernel:Banner:Enabled", "false"))).Build();
        Assert.Same(own, withOwn.Single(static d => d.ServiceType == typeof(StartupBannerOptions)).ImplementationInstance);
    }

    [Fact]
    public void Write_PrintsTheLayoutOfVersionFour_WithHostInformationInTheDefaultLocale()
    {
        var resolver = new RecordingMessageResolver(_templates);
        using var provider = Services(new StartupBannerOptions { ApplicationVersion = "1.2.3" }, new FixedHostEnvironment("Orders", "Staging"), resolver);
        var output = new StringWriter();

        new StartupBannerService(provider).Write(output, outputRedirected: true, new StringWriter(), errorRedirected: true, noColor: false);

        string[] expected =
        [
            "",
            "   _  __ ______  _____    _   __",
            "  | |/ //  _/ / / /   |  / | / /",
            "  |   / / // /_/ / /| | /  |/ /",
            " /   |_/ // __  / ___ |/ /|  /",
            "/_/|_/___/_/ /_/_/  |_/_/ |_/",
            $"XiHan Framework v{FrameworkInfo.Version}",
            "Tagline.",
            "",
            "碧落降恩承淑颜，共挚崎缘挽曦寒。",
            "迁般故事终成忆，谨此葳蕤换思短。",
            "              —— 致她",
            "",
            "Application: Orders v1.2.3",
            "Environment: Staging",
            $"Runtime: {RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})",
            $"OS: {RuntimeInformation.OSDescription}",
            "",
        ];
        Assert.Equal(string.Join(Environment.NewLine, expected), output.ToString());
        // 特性还没启动、进程默认作用域还没设置：标签按解析器的默认区域（空区域）解析
        Assert.All(resolver.RequestedLocales, static locale => Assert.Null(locale));
        Assert.Equal(5, resolver.RequestedLocales.Count);
    }

    [Fact]
    public void Write_WithoutVersionOrHostEnvironment_OmitsThoseLines()
    {
        using var withoutVersion = Services(new StartupBannerOptions(), new FixedHostEnvironment("Orders", "Production"), new RecordingMessageResolver(_templates));
        var output = new StringWriter();
        new StartupBannerService(withoutVersion).Write(output, true, new StringWriter(), true, noColor: false);
        Assert.Contains($"Application: Orders{Environment.NewLine}", output.ToString(), StringComparison.Ordinal);

        using var withoutEnvironment = Services(new StartupBannerOptions { ApplicationVersion = "1.2.3" }, null, new RecordingMessageResolver(_templates));
        output = new StringWriter();
        new StartupBannerService(withoutEnvironment).Write(output, true, new StringWriter(), true, noColor: false);
        Assert.DoesNotContain("Application:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Environment:", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Runtime: ", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WhenDisabled_PrintsNothing()
    {
        using var provider = Services(new StartupBannerOptions { Enabled = false }, new FixedHostEnvironment("Orders", "Staging"), null);
        var output = new StringWriter();
        var error = new StringWriter();

        new StartupBannerService(provider).Write(output, false, error, false, noColor: false);

        Assert.Empty(output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Write_ColorsTheLogoOnlyOnAConsoleThatIsNotRedirected()
    {
        using var provider = Services(new StartupBannerOptions(), new FixedHostEnvironment("Orders", "Staging"), new RecordingMessageResolver(_templates));
        var plain = new StringWriter();
        new StartupBannerService(provider).Write(plain, true, new StringWriter(), true, noColor: false);
        Assert.DoesNotContain("\u001b", plain.ToString(), StringComparison.Ordinal);

        var colored = new StringWriter();
        new StartupBannerService(provider).Write(colored, false, new StringWriter(), true, noColor: false);
        var text = colored.ToString();
        // 第一列是色相 0 的红色，最宽一行的末列是 300° 的品红；每行 Logo 末尾复位
        Assert.Contains("\u001b[38;2;255;0;0m/", text, StringComparison.Ordinal);
        Assert.Contains("\u001b[38;2;255;0;255m_", text, StringComparison.Ordinal);
        Assert.Equal(5, CountOf(text, "\u001b[0m"));
        // 去掉转义序列后与不上色的输出逐字相同，标语与信息行不上色
        Assert.Equal(plain.ToString(), EscapeSequence().Replace(text, string.Empty));

        var noColor = new StringWriter();
        new StartupBannerService(provider).Write(noColor, false, new StringWriter(), false, noColor: true);
        Assert.Equal(plain.ToString(), noColor.ToString());
    }

    [Fact]
    public void Write_ToTheConfiguredOutput_LeavesStandardOutputUntouched()
    {
        var error = new StringWriter();
        using var toError = Services(new StartupBannerOptions { Output = error }, new FixedHostEnvironment("Orders", "Staging"), new RecordingMessageResolver(_templates));
        var output = new StringWriter();

        new StartupBannerService(toError).Write(output, false, error, errorRedirected: false, noColor: false);

        Assert.Empty(output.ToString());
        Assert.Contains("XiHan Framework v", error.ToString(), StringComparison.Ordinal);
        // 写到标准错误时按标准错误自己的重定向状态决定是否上色
        Assert.Contains("\u001b", error.ToString(), StringComparison.Ordinal);

        var custom = new StringWriter();
        using var toCustom = Services(new StartupBannerOptions { Output = custom }, null, null);
        new StartupBannerService(toCustom).Write(output, false, error, false, noColor: false);
        Assert.Empty(output.ToString());
        Assert.DoesNotContain("\u001b", custom.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 登记 Banner 选项、宿主环境与解析器的容器
    /// </summary>
    private static ServiceProvider Services(StartupBannerOptions options, IHostEnvironment? environment, IMessageResolver? resolver)
    {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        if (environment is not null)
        {
            services.AddSingleton(environment);
        }

        if (resolver is not null)
        {
            services.AddSingleton(resolver);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 子串出现的次数
    /// </summary>
    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// SGR 转义序列
    /// </summary>
    [GeneratedRegex("\u001b\\[[0-9;]*m")]
    private static partial Regex EscapeSequence();
}
