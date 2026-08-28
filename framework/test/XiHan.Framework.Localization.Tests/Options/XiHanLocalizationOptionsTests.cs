// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Localization.Options;

namespace XiHan.Framework.Localization.Tests.Options;

/// <summary>
/// 本地化配置选项测试
/// </summary>
/// <remarks>
/// 默认值是整个本地化链路的隐式契约：资源目录、默认资源名、默认文化、请求头名都会被
/// 资源存储、枚举本地化服务和请求文化中间件直接读取，改动会静默改变线上行为，因此逐项锁死。
/// </remarks>
public class XiHanLocalizationOptionsTests
{
    /// <summary>
    /// 配置节名称是对外契约，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableContractValue()
    {
        Assert.Equal("XiHan:Localization", XiHanLocalizationOptions.SectionName);
    }

    /// <summary>
    /// 未做任何配置时的默认值
    /// </summary>
    [Fact]
    public void Defaults_MatchDocumentedContract()
    {
        var options = new XiHanLocalizationOptions();

        Assert.Equal("/Localization", options.ResourcesPath);
        Assert.True(options.EnableDynamicJsonReload);
        Assert.Equal("Default", options.DefaultResourceName);
        Assert.Equal("Enums", options.EnumResourceName);
        Assert.Equal(string.Empty, options.EnumLocalizationKeyPrefix);
        Assert.Equal("zh-CN", options.DefaultCulture);
        Assert.Equal("X-Language", options.CultureHeaderName);
        Assert.True(options.FallbackToParentCultures);
        Assert.True(options.FallbackToDefaultCulture);
    }

    /// <summary>
    /// 默认受支持文化为简体中文与美式英文
    /// </summary>
    [Fact]
    public void Defaults_SupportedCultures_AreChineseAndEnglish()
    {
        var options = new XiHanLocalizationOptions();

        Assert.Equal(["zh-CN", "en-US"], options.SupportedCultures);
    }

    /// <summary>
    /// 两个实例的受支持文化列表互相独立，改一个不会影响另一个
    /// </summary>
    /// <remarks>
    /// 默认集合若被写成静态共享实例，多租户/多测试场景会互相污染，这里显式验证隔离性。
    /// </remarks>
    [Fact]
    public void Defaults_SupportedCultures_AreNotSharedBetweenInstances()
    {
        var first = new XiHanLocalizationOptions();
        var second = new XiHanLocalizationOptions();

        first.SupportedCultures.Add("ja-JP");

        Assert.DoesNotContain("ja-JP", second.SupportedCultures);
    }

    /// <summary>
    /// 从配置节绑定后标量属性被覆盖
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_OverridesScalarDefaults()
    {
        var options = BindFromConfiguration(new Dictionary<string, string?>
        {
            [$"{XiHanLocalizationOptions.SectionName}:ResourcesPath"] = "/i18n",
            [$"{XiHanLocalizationOptions.SectionName}:EnableDynamicJsonReload"] = "false",
            [$"{XiHanLocalizationOptions.SectionName}:DefaultResourceName"] = "Common",
            [$"{XiHanLocalizationOptions.SectionName}:EnumResourceName"] = "Dict",
            [$"{XiHanLocalizationOptions.SectionName}:EnumLocalizationKeyPrefix"] = "App",
            [$"{XiHanLocalizationOptions.SectionName}:DefaultCulture"] = "en-US",
            [$"{XiHanLocalizationOptions.SectionName}:CultureHeaderName"] = "X-Culture",
            [$"{XiHanLocalizationOptions.SectionName}:FallbackToParentCultures"] = "false",
            [$"{XiHanLocalizationOptions.SectionName}:FallbackToDefaultCulture"] = "false"
        });

        Assert.Equal("/i18n", options.ResourcesPath);
        Assert.False(options.EnableDynamicJsonReload);
        Assert.Equal("Common", options.DefaultResourceName);
        Assert.Equal("Dict", options.EnumResourceName);
        Assert.Equal("App", options.EnumLocalizationKeyPrefix);
        Assert.Equal("en-US", options.DefaultCulture);
        Assert.Equal("X-Culture", options.CultureHeaderName);
        Assert.False(options.FallbackToParentCultures);
        Assert.False(options.FallbackToDefaultCulture);
    }

    /// <summary>
    /// 配置里声明的受支持文化能被绑定进来
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_AddsConfiguredSupportedCulture()
    {
        var options = BindFromConfiguration(new Dictionary<string, string?>
        {
            [$"{XiHanLocalizationOptions.SectionName}:SupportedCultures:0"] = "ja-JP"
        });

        Assert.Contains("ja-JP", options.SupportedCultures);
    }

    /// <summary>
    /// 未提供配置节时保持默认值
    /// </summary>
    [Fact]
    public void Bind_WhenSectionAbsent_KeepsDefaults()
    {
        var options = BindFromConfiguration(new Dictionary<string, string?>
        {
            ["SomethingElse:Value"] = "1"
        });

        Assert.Equal("/Localization", options.ResourcesPath);
        Assert.Equal("zh-CN", options.DefaultCulture);
    }

    private static XiHanLocalizationOptions BindFromConfiguration(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.Configure<XiHanLocalizationOptions>(configuration.GetSection(XiHanLocalizationOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<XiHanLocalizationOptions>>().Value;
    }
}
