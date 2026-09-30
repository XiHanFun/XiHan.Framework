// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 发件箱待送事件计数器默认实现
/// </summary>
/// <remarks>
/// 对每个不同的发件箱实现类型，在独立作用域内切换到目标租户后统计；同一实现类型只统计一次。
/// </remarks>
public class OutboxPendingEventCounter : IOutboxPendingEventCounter
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<XiHanDistributedEventBusOptions> _distributedOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="distributedOptions">分布式事件总线选项</param>
    public OutboxPendingEventCounter(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<XiHanDistributedEventBusOptions> distributedOptions)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _distributedOptions = distributedOptions;
    }

    /// <summary>
    /// 统计指定投递目标在全部按租户定位存储的发件箱（实现 <see cref="ITenantScopedEventOutbox"/>）中尚未删除的事件数
    /// </summary>
    /// <param name="target">投递目标</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>尚未删除的事件数</returns>
    /// <exception cref="NotSupportedException">没有任何已配置的发件箱实现 <see cref="ITenantScopedEventOutbox"/></exception>
    public virtual async Task<long> GetPendingCountAsync(
        OutboxDeliveryTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        var implementationTypes = _distributedOptions.Value.Outboxes.Values
            .Select(config => config.ImplementationType)
            .Where(type => type is not null)
            .Distinct()
            .ToArray();

        long total = 0;
        var supported = false;

        foreach (var implementationType in implementationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var scope = _serviceScopeFactory.CreateScope();
            var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

            using (currentTenant.Change(target.TenantId, target.TenantName))
            {
                if (scope.ServiceProvider.GetService(implementationType) is not ITenantScopedEventOutbox outbox)
                {
                    continue;
                }

                supported = true;
                total += await outbox.GetPendingCountAsync(cancellationToken);
            }
        }

        if (!supported)
        {
            throw new NotSupportedException("没有任何已配置的发件箱按租户定位存储，无法统计投递目标的待送事件数。");
        }

        return total;
    }
}
