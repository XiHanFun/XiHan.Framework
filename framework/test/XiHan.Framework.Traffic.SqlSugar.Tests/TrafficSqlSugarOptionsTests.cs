// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Traffic.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Traffic.SqlSugar.Options;

namespace XiHan.Framework.Traffic.SqlSugar.Tests;

/// <summary>
/// 流量治理 SqlSugar 选项测试
/// </summary>
public class TrafficSqlSugarOptionsTests
{
    /// <summary>
    /// 刷新间隔从配置节绑定
    /// </summary>
    [Fact]
    public void 刷新间隔从配置节绑定()
    {
        using var provider = BuildProvider("00:00:10");

        Assert.Equal(TimeSpan.FromSeconds(10), provider.GetRequiredService<IOptions<XiHanTrafficSqlSugarOptions>>().Value.RefreshInterval);
    }

    /// <summary>
    /// 刷新间隔不大于零时读取选项失败
    /// </summary>
    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:05")]
    public void 刷新间隔不大于零时读取选项失败(string value)
    {
        using var provider = BuildProvider(value);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<XiHanTrafficSqlSugarOptions>>().Value);
    }

    /// <summary>
    /// 刷新间隔不大于零时启动校验失败
    /// </summary>
    [Fact]
    public void 刷新间隔不大于零时启动校验失败()
    {
        using var provider = BuildProvider("00:00:00");

        var validator = provider.GetRequiredService<IStartupValidator>();

        Assert.Throws<OptionsValidationException>(validator.Validate);
    }

    private static ServiceProvider BuildProvider(string refreshInterval)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{XiHanTrafficSqlSugarOptions.SectionName}:{nameof(XiHanTrafficSqlSugarOptions.RefreshInterval)}"] = refreshInterval
            })
            .Build();

        var services = new ServiceCollection();
        services.AddXiHanTrafficSqlSugar(configuration);

        return services.BuildServiceProvider();
    }
}
