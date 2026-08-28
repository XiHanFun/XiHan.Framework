// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Tests.Options;

/// <summary>
/// MCP Server 配置选项测试
/// </summary>
/// <remarks>
/// 这个选项类是 /mcp 端点唯一的门控开关：<see cref="XiHanMcpOptions.IsExposable"/> 为 false 时
/// 既不注册 MCP 服务也不映射端点。默认值一旦从 fail-closed 漂成 fail-open，
/// 就等于把未鉴权的 MCP 工具集直接暴露到公网，因此默认值与判定矩阵在这里逐条锁死。
/// 配置节名被 appsettings 依赖，属对外契约，同样锁死。
/// </remarks>
public class XiHanMcpOptionsTests
{
    /// <summary>
    /// 配置节名是对外契约，不得随实现改动漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationKey()
    {
        Assert.Equal("XiHan:AI:Mcp", XiHanMcpOptions.SectionName);
    }

    /// <summary>
    /// 全新实例的默认值必须是 fail-closed 的：未启用、无密钥、不可暴露
    /// </summary>
    [Fact]
    public void Defaults_AreFailClosed()
    {
        var options = new XiHanMcpOptions();

        Assert.False(options.Enabled);
        Assert.Null(options.ApiKey);
        Assert.False(options.IsExposable);
    }

    /// <summary>
    /// 未显式配置时的传输默认值：默认请求头名、默认端点路径、默认无状态
    /// </summary>
    [Fact]
    public void Defaults_UseConventionalTransportSettings()
    {
        var options = new XiHanMcpOptions();

        Assert.Equal("X-Api-Key", options.HeaderName);
        Assert.Equal("/mcp", options.Path);
        Assert.True(options.Stateless);
    }

    /// <summary>
    /// 可暴露判定矩阵：启用与非空白密钥两个条件必须同时满足
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">配置的密钥</param>
    /// <param name="expected">期望的可暴露判定</param>
    [Theory]
    [InlineData(false, null, false)]
    [InlineData(false, "", false)]
    [InlineData(false, "valid-key", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "", false)]
    [InlineData(true, "   ", false)]
    [InlineData(true, "\t", false)]
    [InlineData(true, "valid-key", true)]
    [InlineData(true, " padded-key ", true)]
    public void IsExposable_RequiresEnabledAndNonBlankApiKey(bool enabled, string? apiKey, bool expected)
    {
        var options = new XiHanMcpOptions
        {
            Enabled = enabled,
            ApiKey = apiKey
        };

        Assert.Equal(expected, options.IsExposable);
    }

    /// <summary>
    /// 可暴露判定是实时计算的，密钥被后续改写后判定随之变化
    /// </summary>
    [Fact]
    public void IsExposable_ReflectsLaterMutation()
    {
        var options = new XiHanMcpOptions { Enabled = true, ApiKey = "valid-key" };

        Assert.True(options.IsExposable);

        options.ApiKey = null;

        Assert.False(options.IsExposable);
    }

    /// <summary>
    /// 配置节能完整绑定到选项的每个可写属性
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_MapsEveryProperty()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{XiHanMcpOptions.SectionName}:Enabled"] = "true",
            [$"{XiHanMcpOptions.SectionName}:ApiKey"] = "configured-key",
            [$"{XiHanMcpOptions.SectionName}:HeaderName"] = "X-Mcp-Key",
            [$"{XiHanMcpOptions.SectionName}:Path"] = "/internal/mcp",
            [$"{XiHanMcpOptions.SectionName}:Stateless"] = "false"
        });

        var options = configuration.GetSection(XiHanMcpOptions.SectionName).Get<XiHanMcpOptions>();

        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal("configured-key", options.ApiKey);
        Assert.Equal("X-Mcp-Key", options.HeaderName);
        Assert.Equal("/internal/mcp", options.Path);
        Assert.False(options.Stateless);
        Assert.True(options.IsExposable);
    }

    /// <summary>
    /// 只配了部分键时，其余键保持默认值
    /// </summary>
    [Fact]
    public void Bind_FromPartialSection_KeepsDefaultsForMissingKeys()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{XiHanMcpOptions.SectionName}:Enabled"] = "true",
            [$"{XiHanMcpOptions.SectionName}:ApiKey"] = "configured-key"
        });

        var options = configuration.GetSection(XiHanMcpOptions.SectionName).Get<XiHanMcpOptions>();

        Assert.NotNull(options);
        Assert.Equal("X-Api-Key", options.HeaderName);
        Assert.Equal("/mcp", options.Path);
        Assert.True(options.Stateless);
    }

    /// <summary>
    /// 配置里完全没有该节时绑定结果为 null，服务注册端据此回落到默认实例
    /// </summary>
    /// <remarks>
    /// <c>AddXiHanWebMcp</c> 写的是 <c>section.Get&lt;XiHanMcpOptions&gt;() ?? new XiHanMcpOptions()</c>，
    /// 这里锁住 null 这一半的前提，缺了它那句空合并就成了永远走不到的死代码。
    /// </remarks>
    [Fact]
    public void Bind_FromAbsentSection_ReturnsNull()
    {
        var configuration = BuildConfiguration([]);

        var options = configuration.GetSection(XiHanMcpOptions.SectionName).Get<XiHanMcpOptions>();

        Assert.Null(options);
    }

    /// <summary>
    /// 只配了启用开关而没配密钥时，绑定结果仍判定为不可暴露
    /// </summary>
    [Fact]
    public void Bind_WithEnabledButNoApiKey_StaysNotExposable()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{XiHanMcpOptions.SectionName}:Enabled"] = "true"
        });

        var options = configuration.GetSection(XiHanMcpOptions.SectionName).Get<XiHanMcpOptions>();

        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.False(options.IsExposable);
    }

    /// <summary>
    /// 构造内存配置
    /// </summary>
    /// <param name="settings">配置键值</param>
    /// <returns>配置根</returns>
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
