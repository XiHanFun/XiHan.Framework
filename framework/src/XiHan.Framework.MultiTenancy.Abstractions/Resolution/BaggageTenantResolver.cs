// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Resolution;

/// <summary>
/// 从 Baggage 的 <see cref="BaggageKeys.Tenant"/> 解析租户
/// </summary>
/// <remarks>
/// 值由进程内建上下文的地方放进去：消息派发、作业、工作流、命令行宿主。HTTP、gRPC、MCP 的入站不收这个键（ADR-0121），这些调用方选不了租户；CloudEvents 入站的 <c>xihantenant</c> 经消息派发进来，由宿主给端点加的权限把关
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class BaggageTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public ValueTask<Result<Maybe<string>>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 这里只取值不信任；是否存在、是否启用由中间件对照存储核对
        return ValueTask.FromResult(Result.Success(context.TryGetBaggage(BaggageKeys.Tenant, out var tenantId) ? Maybe.Some(tenantId) : Maybe.None<string>()));
    }
}
