// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text.Json;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Exceptions.Handling;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Threading;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 组装 <see cref="DistributedCache{TCacheItem, TCacheKey}"/> 所需协作者的测试上下文
/// </summary>
/// <remarks>
/// 键规范化器与序列化器都用真实实现，只把「缓存后端」「租户」「异常通知」换成替身，
/// 这样断言到的规范化键就是生产路径上真正会写进后端的键。
/// </remarks>
internal sealed class DistributedCacheTestContext : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner">缓存后端替身</param>
    /// <param name="options">分布式缓存选项，缺省时用默认值</param>
    /// <param name="tenant">当前租户，缺省时为无租户</param>
    public DistributedCacheTestContext(IDistributedCache inner, XiHanDistributedCacheOptions? options = null, BasicTenantInfo? tenant = null)
    {
        Inner = inner;
        Options = options ?? new XiHanDistributedCacheOptions();
        Notifier = new RecordingExceptionNotifier();

        var services = new ServiceCollection();
        services.AddSingleton<IExceptionNotifier>(Notifier);
        services.AddSingleton<ICurrentTenantAccessor>(new FakeCurrentTenantAccessor { Current = tenant });
        Provider = services.BuildServiceProvider();

        KeyNormalizer = new DefaultDistributedCacheKeyNormalizer(Provider);
        Serializer = new JsonDistributedCacheSerializer(
            Microsoft.Extensions.Options.Options.Create(new JsonSerializerOptions()));
    }

    /// <summary>
    /// 缓存后端替身
    /// </summary>
    public IDistributedCache Inner { get; }

    /// <summary>
    /// 分布式缓存选项
    /// </summary>
    public XiHanDistributedCacheOptions Options { get; }

    /// <summary>
    /// 异常通知记录器
    /// </summary>
    public RecordingExceptionNotifier Notifier { get; }

    /// <summary>
    /// 服务提供者
    /// </summary>
    public ServiceProvider Provider { get; }

    /// <summary>
    /// 键规范化器
    /// </summary>
    public IDistributedCacheKeyNormalizer KeyNormalizer { get; }

    /// <summary>
    /// 序列化器
    /// </summary>
    public IDistributedCacheSerializer Serializer { get; }

    /// <summary>
    /// 创建指定缓存项与键类型的分布式缓存
    /// </summary>
    /// <typeparam name="TCacheItem">缓存项类型</typeparam>
    /// <typeparam name="TCacheKey">缓存键类型</typeparam>
    /// <returns>分布式缓存</returns>
    public DistributedCache<TCacheItem, TCacheKey> Create<TCacheItem, TCacheKey>()
        where TCacheItem : class
        where TCacheKey : notnull
    {
        return new DistributedCache<TCacheItem, TCacheKey>(
            Microsoft.Extensions.Options.Options.Create(Options),
            Inner,
            NullCancellationTokenProvider.Instance,
            Serializer,
            KeyNormalizer,
            Provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeUnitOfWorkManager());
    }

    /// <summary>
    /// 创建字符串键的分布式缓存
    /// </summary>
    /// <typeparam name="TCacheItem">缓存项类型</typeparam>
    /// <returns>分布式缓存</returns>
    public DistributedCache<TCacheItem, string> CreateStringKeyed<TCacheItem>()
        where TCacheItem : class
    {
        return Create<TCacheItem, string>();
    }

    /// <summary>
    /// 释放内部容器
    /// </summary>
    public void Dispose()
    {
        Provider.Dispose();
    }
}

/// <summary>
/// 记录被通知异常的异常通知器
/// </summary>
internal sealed class RecordingExceptionNotifier : IExceptionNotifier
{
    private readonly ConcurrentBag<Exception> _exceptions = [];

    /// <summary>
    /// 已记录的异常
    /// </summary>
    public IReadOnlyCollection<Exception> Exceptions => [.. _exceptions];

    /// <summary>
    /// 记录异常
    /// </summary>
    /// <param name="context">异常通知上下文</param>
    /// <returns>异步任务</returns>
    public Task NotifyAsync(ExceptionNotificationContext context)
    {
        _exceptions.Add(context.Exception);

        return Task.CompletedTask;
    }
}

/// <summary>
/// 可直接设置当前租户的访问器替身
/// </summary>
internal sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
{
    /// <summary>
    /// 当前租户
    /// </summary>
    public BasicTenantInfo? Current { get; set; }
}

/// <summary>
/// 永远没有活跃工作单元的工作单元管理器替身
/// </summary>
/// <remarks>
/// 缓存的 considerUow 分支需要真实工作单元基础设施，本替身只服务于 considerUow=false 的直连路径。
/// </remarks>
internal sealed class FakeUnitOfWorkManager : IUnitOfWorkManager
{
    /// <summary>
    /// 当前工作单元，恒为空
    /// </summary>
    public IUnitOfWork? Current => null;

    /// <summary>
    /// 开启工作单元，替身不支持
    /// </summary>
    /// <param name="options">工作单元选项</param>
    /// <param name="requiresNew">是否要求新建</param>
    /// <returns>不会返回</returns>
    public IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false)
    {
        throw new NotSupportedException("测试替身不提供工作单元。");
    }

    /// <summary>
    /// 预留工作单元，替身不支持
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="requiresNew">是否要求新建</param>
    /// <returns>不会返回</returns>
    public IUnitOfWork Reserve(string reservationName, bool requiresNew = false)
    {
        throw new NotSupportedException("测试替身不提供工作单元。");
    }

    /// <summary>
    /// 开启预留工作单元，替身不支持
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    public void BeginReserved(string reservationName, XiHanUnitOfWorkOptions options)
    {
        throw new NotSupportedException("测试替身不提供工作单元。");
    }

    /// <summary>
    /// 尝试开启预留工作单元，替身恒为失败
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    /// <returns>恒为 false</returns>
    public bool TryBeginReserved(string reservationName, XiHanUnitOfWorkOptions options)
    {
        return false;
    }
}

/// <summary>
/// 带缓存名称标注的测试缓存项，规范化键中的缓存名段固定为 sample
/// </summary>
[CacheName("sample")]
public sealed class SampleCacheItem
{
    /// <summary>
    /// 值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 标注了忽略多租户的测试缓存项，规范化键中的缓存名段固定为 neutral
/// </summary>
[CacheName("neutral")]
[IgnoreMultiTenancy]
public sealed class TenantNeutralCacheItem
{
    /// <summary>
    /// 值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 未标注缓存名称的测试缓存项，用于验证按类型全名推导的约定
/// </summary>
public sealed class PlainSampleCacheItem
{
    /// <summary>
    /// 值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
