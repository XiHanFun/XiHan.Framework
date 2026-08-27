// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Caching.Options;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 曦寒 Redis 缓存配置选项测试
/// </summary>
/// <remarks>
/// 配置节名与属性名都是部署侧 appsettings 的对外契约，改名会让线上配置静默失效并悄悄退回默认值，
/// 所以这里把节名与每一项默认值都锁死，并验证整节能被完整绑定。
/// </remarks>
public class XiHanRedisCacheOptionsTests
{
    /// <summary>
    /// 配置节名保持不变
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:Caching:Redis", XiHanRedisCacheOptions.SectionName);
    }

    /// <summary>
    /// 默认不启用 Redis，退回进程内实现
    /// </summary>
    /// <remarks>
    /// 默认关闭是安全侧的选择：没配连接串就不该在启动时尝试连一个不存在的 Redis。
    /// </remarks>
    [Fact]
    public void IsEnabled_DefaultsToFalse()
    {
        Assert.False(new XiHanRedisCacheOptions().IsEnabled);
    }

    /// <summary>
    /// 其余选项的默认值
    /// </summary>
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var options = new XiHanRedisCacheOptions();

        Assert.Equal(string.Empty, options.Configuration);
        Assert.Null(options.InstanceName);
        Assert.Equal(5000, options.ConnectTimeout);
        Assert.Equal(5000, options.SyncTimeout);
        Assert.Equal(5000, options.AsyncTimeout);
        Assert.False(options.AllowAdmin);
        Assert.False(options.UseSsl);
        Assert.False(options.AbortOnConnectFail);
    }

    /// <summary>
    /// 配置节里的每一项都能被绑定
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_PopulatesEveryOption()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{XiHanRedisCacheOptions.SectionName}:IsEnabled"] = "true",
                [$"{XiHanRedisCacheOptions.SectionName}:Configuration"] = "127.0.0.1:6379",
                [$"{XiHanRedisCacheOptions.SectionName}:InstanceName"] = "xihan",
                [$"{XiHanRedisCacheOptions.SectionName}:ConnectTimeout"] = "1000",
                [$"{XiHanRedisCacheOptions.SectionName}:SyncTimeout"] = "2000",
                [$"{XiHanRedisCacheOptions.SectionName}:AsyncTimeout"] = "3000",
                [$"{XiHanRedisCacheOptions.SectionName}:AllowAdmin"] = "true",
                [$"{XiHanRedisCacheOptions.SectionName}:UseSsl"] = "true",
                [$"{XiHanRedisCacheOptions.SectionName}:AbortOnConnectFail"] = "true"
            })
            .Build();

        var options = configuration.GetSection(XiHanRedisCacheOptions.SectionName).Get<XiHanRedisCacheOptions>();

        Assert.NotNull(options);
        Assert.True(options.IsEnabled);
        Assert.Equal("127.0.0.1:6379", options.Configuration);
        Assert.Equal("xihan", options.InstanceName);
        Assert.Equal(1000, options.ConnectTimeout);
        Assert.Equal(2000, options.SyncTimeout);
        Assert.Equal(3000, options.AsyncTimeout);
        Assert.True(options.AllowAdmin);
        Assert.True(options.UseSsl);
        Assert.True(options.AbortOnConnectFail);
    }

    /// <summary>
    /// 配置节缺失时绑定结果为空，由调用方回落到默认实例
    /// </summary>
    [Fact]
    public void Bind_FromMissingSection_ReturnsNull()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Null(configuration.GetSection(XiHanRedisCacheOptions.SectionName).Get<XiHanRedisCacheOptions>());
    }
}
