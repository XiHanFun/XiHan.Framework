// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 记下发布时所在调用的消息发布器桩
/// </summary>
/// <remarks>
/// 按作用域登记：经作用域的 <see cref="IInvocationAccessor"/> 看发布是不是在某个调用里，记下那个调用的租户与主体 Baggage
/// </remarks>
internal sealed class ContextPublisher(IInvocationAccessor invocation, ConcurrentQueue<(string? Tenant, string? Principal)> published) : IMessagePublisher
{
    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        Record();
        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        foreach (var _ in envelopes)
        {
            Record();
        }

        return ValueTask.FromResult<IReadOnlyList<Result>>(envelopes.Select(static _ => Result.Success()).ToList());
    }

    /// <summary>
    /// 记一次发布所在调用的租户与主体
    /// </summary>
    /// <remarks>
    /// 不在调用里时两者都为空
    /// </remarks>
    private void Record()
    {
        var context = invocation.Current.TryGetValue(out var current) ? current : null;
        published.Enqueue((Baggage(context, BaggageKeys.Tenant), Baggage(context, BaggageKeys.Principal)));
    }

    /// <summary>
    /// 调用里的一个 Baggage 值
    /// </summary>
    private static string? Baggage(InvocationContext? context, string key)
        => context is not null && context.TryGetBaggage(key, out var value) ? value : null;
}
