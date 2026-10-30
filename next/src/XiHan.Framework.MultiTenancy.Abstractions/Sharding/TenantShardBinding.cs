// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 把当前调用的租户分片表示成连接绑定。
/// </summary>
/// <remarks>
/// 供持久化 Provider 在作用域内创建客户端时替换应用默认连接。绑定主值是分片连接串，属性并入分片属性并带 <c>tenant</c> 与 <c>provider</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class TenantShardBinding
{
    /// <summary>
    /// 作用域带分片时返回分片绑定，否则返回 <see cref="Maybe.None{T}"/>。
    /// </summary>
    /// <remarks>
    /// 先看 <see cref="TenantShardScope"/> 的显式绑定，再看 Tenant 阶段写进当前调用上下文的分片；不触存储、不阻塞。持久化 Provider 在创建客户端时调用，此后作用域不能再绑定分片（ADR-0057）。
    /// </remarks>
    public static Maybe<BindingValue> Resolve(IServiceProvider scopedServices, string bindingName, string bindingType, Maybe<BindingValue> fallback)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingType);
        var scope = scopedServices.GetService<TenantShardScope>();
        scope?.MarkConnected();
        var chosen = scope is { Shard.HasValue: true } ? scope.Shard : InvocationShard(scopedServices);
        if (!chosen.TryGetValue(out var shard))
        {
            return Maybe.None<BindingValue>();
        }

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (fallback.TryGetValue(out var original))
        {
            foreach (var (key, value) in original.Properties)
            {
                properties[key] = value;
            }
        }

        foreach (var (key, value) in shard.Properties)
        {
            properties[key] = value;
        }

        properties[TenantShardKeys.TenantProperty] = shard.TenantId;
        if (shard.Provider is not null)
        {
            properties[TenantShardKeys.ProviderProperty] = shard.Provider;
        }

        return Maybe.Some(new BindingValue(original?.Name ?? bindingName, original?.Type ?? bindingType, shard.ConnectionString, properties));
    }

    /// <summary>
    /// 当前调用上下文里的分片。
    /// </summary>
    private static Maybe<TenantShard> InvocationShard(IServiceProvider scopedServices)
        => scopedServices.GetService<IInvocationAccessor>() is { } accessor && accessor.Current.TryGetValue(out var context) ? context.GetTenantShard() : Maybe.None<TenantShard>();
}
