// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 调用上下文里的租户分片：Tenant 阶段写入，之后的阶段与持久化 Provider 只读。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class TenantShardInvocationExtensions
{
    /// <summary>
    /// 分片在上下文条目里的键。
    /// </summary>
    private static readonly ContextItemKey<TenantShard> _shardKey = new("xihan.tenant.shard");

    /// <summary>
    /// 当前调用的租户分片。
    /// </summary>
    /// <remarks>
    /// 没有分片（共库租户或无租户）为空。
    /// </remarks>
    public static Maybe<TenantShard> GetTenantShard(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_shardKey);
    }

    /// <summary>
    /// 写入当前调用的租户分片。
    /// </summary>
    public static void SetTenantShard(this InvocationContext context, TenantShard shard)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shard);
        context.Set(_shardKey, shard);
    }
}
