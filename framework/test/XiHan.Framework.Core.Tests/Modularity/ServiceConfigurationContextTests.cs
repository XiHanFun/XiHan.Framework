// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 服务配置上下文测试
/// </summary>
/// <remarks>
/// 上下文在整个服务配置阶段被所有模块共享，Items 是模块之间传递中间产物的唯一通道，
/// 索引器取不到键时必须返回空而不是抛出，否则「后一个模块检查前一个模块是否放过东西」的惯用法会失效。
/// </remarks>
public class ServiceConfigurationContextTests
{
    /// <summary>
    /// 构造后持有传入的服务集合且存储器为空
    /// </summary>
    [Fact]
    public void Constructor_KeepsServicesAndStartsWithEmptyItems()
    {
        IServiceCollection services = new ServiceCollection();

        var context = new ServiceConfigurationContext(services);

        Assert.Same(services, context.Services);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 服务集合为空时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new ServiceConfigurationContext(null!);
        });
    }

    /// <summary>
    /// 索引器取不到键时返回空
    /// </summary>
    [Fact]
    public void Indexer_WhenKeyMissing_ReturnsNull()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());

        Assert.Null(context["absent"]);
    }

    /// <summary>
    /// 索引器写入的值可读回且落进存储器
    /// </summary>
    [Fact]
    public void Indexer_WhenValueSet_IsReadableFromItems()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var payload = new object();

        context["payload"] = payload;

        Assert.Same(payload, context["payload"]);
        Assert.Same(payload, context.Items["payload"]);
    }

    /// <summary>
    /// 索引器重复写入同一键时覆盖旧值
    /// </summary>
    [Fact]
    public void Indexer_WhenSetTwice_OverwritesPreviousValue()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());

        context["payload"] = "first";
        context["payload"] = "second";

        Assert.Equal("second", context["payload"]);
        Assert.Single(context.Items);
    }

    /// <summary>
    /// 索引器允许写入空值并读回空
    /// </summary>
    [Fact]
    public void Indexer_WhenValueIsNull_KeepsKeyWithNullValue()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());

        context["payload"] = null;

        Assert.Null(context["payload"]);
        Assert.True(context.Items.ContainsKey("payload"));
    }

    /// <summary>
    /// 经上下文注册的服务落在原服务集合上
    /// </summary>
    [Fact]
    public void Services_RegistrationsFlowIntoOriginalCollection()
    {
        IServiceCollection services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);

        context.Services.AddSingleton<ISccContract, SccService>();

        Assert.Contains(services, d => d.ServiceType == typeof(ISccContract));
    }
}

/// <summary>
/// 服务配置上下文测试用契约
/// </summary>
internal interface ISccContract;

/// <summary>
/// 服务配置上下文测试用实现
/// </summary>
internal class SccService : ISccContract;
