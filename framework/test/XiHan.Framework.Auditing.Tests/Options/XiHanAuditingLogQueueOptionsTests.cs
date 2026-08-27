// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Options;

namespace XiHan.Framework.Auditing.Tests.Options;

/// <summary>
/// 审计日志队列选项测试
/// </summary>
/// <remarks>
/// 选项类没有 Validate，默认值本身就是契约：五个队列开关默认全关（不配置＝同步写入，行为可预期）、
/// 满时默认反压而非丢弃（不静默丢审计）、容量与批量参数决定内存占用与落库节奏。
/// 配置节名同样是对外契约，appsettings 里写错一个字就静默失效，所以用真实配置键做一次端到端绑定验证。
/// </remarks>
public class XiHanAuditingLogQueueOptionsTests
{
    /// <summary>
    /// 配置节名称不允许漂移，否则线上配置会静默失效
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationKey()
    {
        Assert.Equal("XiHan:Auditing:LogQueue", XiHanAuditingLogQueueOptions.SectionName);
    }

    /// <summary>
    /// 默认值：队列全关、满时反压、容量 10000、批量 100 条 / 200 毫秒、忽略 /hubs 前缀
    /// </summary>
    [Fact]
    public void Defaults_MatchDocumentedContract()
    {
        var options = new XiHanAuditingLogQueueOptions();

        Assert.False(options.EnableAccessLogQueue);
        Assert.False(options.EnableOperationLogQueue);
        Assert.False(options.EnableExceptionLogQueue);
        Assert.False(options.EnableApiLogQueue);
        Assert.False(options.EnableLoginLogQueue);

        // 默认反压而不是丢弃：审计日志宁可拖慢请求也不静默丢失
        Assert.False(options.DropOnFull);

        Assert.Equal(10000, options.QueueCapacity);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(200, options.BatchDelayMilliseconds);

        Assert.Equal("/hubs", Assert.Single(options.IgnoredPathPrefixes));
    }

    /// <summary>
    /// 每个选项实例持有独立的忽略前缀数组，修改一个不会污染另一个
    /// </summary>
    [Fact]
    public void IgnoredPathPrefixes_IsNotSharedBetweenInstances()
    {
        var first = new XiHanAuditingLogQueueOptions();
        var second = new XiHanAuditingLogQueueOptions();

        Assert.NotSame(first.IgnoredPathPrefixes, second.IgnoredPathPrefixes);

        first.IgnoredPathPrefixes[0] = "/changed";

        Assert.Equal("/hubs", second.IgnoredPathPrefixes[0]);
    }

    /// <summary>
    /// 用真实配置键绑定后覆盖标量默认值，未配置项保持默认
    /// </summary>
    [Fact]
    public void Binding_FromRealConfigurationKeys_OverridesScalarDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Auditing:LogQueue:EnableAccessLogQueue"] = "true",
                ["XiHan:Auditing:LogQueue:DropOnFull"] = "true",
                ["XiHan:Auditing:LogQueue:QueueCapacity"] = "42",
                ["XiHan:Auditing:LogQueue:BatchSize"] = "7",
                ["XiHan:Auditing:LogQueue:BatchDelayMilliseconds"] = "9"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<XiHanAuditingLogQueueOptions>(
            configuration.GetSection(XiHanAuditingLogQueueOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanAuditingLogQueueOptions>>().Value;

        Assert.True(options.EnableAccessLogQueue);
        Assert.True(options.DropOnFull);
        Assert.Equal(42, options.QueueCapacity);
        Assert.Equal(7, options.BatchSize);
        Assert.Equal(9, options.BatchDelayMilliseconds);

        // 未出现在配置中的开关保持默认关闭
        Assert.False(options.EnableApiLogQueue);
        Assert.False(options.EnableLoginLogQueue);
    }
}
