// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.SqlSugar.Options;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 任务 SqlSugar 存储配置测试
/// </summary>
public class XiHanTasksSqlSugarOptionsTests
{
    /// <summary>
    /// 后台作业租约默认五分钟
    /// </summary>
    [Fact]
    public void 后台作业租约默认五分钟()
    {
        var options = new XiHanTasksSqlSugarOptions();

        Assert.Equal(TimeSpan.FromMinutes(5), options.BackgroundJobLeaseTimeout);
    }

    /// <summary>
    /// 配置节名称带框架前缀
    /// </summary>
    [Fact]
    public void 配置节名称带框架前缀()
    {
        Assert.Equal("XiHan:Tasks:SqlSugar", XiHanTasksSqlSugarOptions.SectionName);
    }

    /// <summary>
    /// 从配置节绑定租约时长
    /// </summary>
    [Fact]
    public void 从配置节绑定租约时长()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Tasks:SqlSugar:BackgroundJobLeaseTimeout"] = "00:10:00"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddXiHanTasksSqlSugar(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanTasksSqlSugarOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(10), options.BackgroundJobLeaseTimeout);
    }

    /// <summary>
    /// 运行中实例宽限期默认一分钟
    /// </summary>
    [Fact]
    public void 运行中实例宽限期默认一分钟()
    {
        var options = new XiHanTasksSqlSugarOptions();

        Assert.Equal(TimeSpan.FromMinutes(1), options.RunningInstanceGracePeriod);
    }

    /// <summary>
    /// 从配置节绑定宽限期
    /// </summary>
    [Fact]
    public void 从配置节绑定宽限期()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Tasks:SqlSugar:RunningInstanceGracePeriod"] = "00:03:00"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddXiHanTasksSqlSugar(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanTasksSqlSugarOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(3), options.RunningInstanceGracePeriod);
    }
    /// <summary>
    /// 领取批量上限默认五十
    /// </summary>
    [Fact]
    public void 领取批量上限默认五十()
    {
        Assert.Equal(50, new XiHanTasksSqlSugarOptions().MaxClaimBatchSize);
    }

    /// <summary>
    /// 从配置节绑定领取批量上限
    /// </summary>
    [Fact]
    public void 从配置节绑定领取批量上限()
    {
        using var provider = BuildProvider("XiHan:Tasks:SqlSugar:MaxClaimBatchSize", "20");

        Assert.Equal(20, provider.GetRequiredService<IOptions<XiHanTasksSqlSugarOptions>>().Value.MaxClaimBatchSize);
    }

    /// <summary>
    /// 领取批量上限不大于零时读取选项失败
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void 领取批量上限不大于零时读取选项失败(string value)
    {
        using var provider = BuildProvider("XiHan:Tasks:SqlSugar:MaxClaimBatchSize", value);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<XiHanTasksSqlSugarOptions>>().Value);
    }

    /// <summary>
    /// 领取批量上限不大于零时启动校验失败
    /// </summary>
    [Fact]
    public void 领取批量上限不大于零时启动校验失败()
    {
        using var provider = BuildProvider("XiHan:Tasks:SqlSugar:MaxClaimBatchSize", "0");

        var validator = provider.GetRequiredService<IStartupValidator>();

        Assert.Throws<OptionsValidationException>(validator.Validate);
    }

    /// <summary>
    /// 后台作业租约时长不大于零时读取选项失败
    /// </summary>
    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    public void 后台作业租约时长不大于零时读取选项失败(string value)
    {
        using var provider = BuildProvider("XiHan:Tasks:SqlSugar:BackgroundJobLeaseTimeout", value);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<XiHanTasksSqlSugarOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();
        var services = new ServiceCollection();

        services.AddXiHanTasksSqlSugar(configuration);

        return services.BuildServiceProvider();
    }
}
