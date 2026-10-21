// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// Idempotency 阶段的中间件：Baggage 里有 <see cref="BaggageKeys.IdempotencyKey"/> 时，按（租户、主体、键）在 <see cref="ISharedCache"/> 的存储里去重。
/// 首次调用写入"处理中"标记并执行，完成后用调用结果替换标记；处理中的重复调用返回 <see cref="CachingErrors.IdempotentInProgress"/>；
/// 已完成的重复调用直接回放结果，不执行处理器。处理器抛出异常时清除标记，允许重试。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class IdempotencyMiddleware(IdempotencyOptions options) : IPipelineMiddleware
{
    private static readonly ContextItemKey<bool> _replayedKey = new("xihan.idempotency.replayed");

    private readonly IdempotencyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 本次调用的结果是否来自回放。
    /// </summary>
    public static bool WasReplayed(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_replayedKey).ValueOr(false);
    }

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);
        if (!context.TryGetBaggage(BaggageKeys.IdempotencyKey, out var idempotencyKey))
        {
            await nextStep(context).ConfigureAwait(false);
            return;
        }

        var security = context.GetSecurity();
        var principalId = security.Principal.Id.Length == 0 ? "-" : security.Principal.Id;
        var scoped = $"idem:{security.Tenant?.Id ?? "-"}:{principalId}:{idempotencyKey}";
        if (!CacheKey.IsValid(scoped))
        {
            context.SetOutcome(Error.Validation(CachingErrors.InvalidKey, "幂等键不合法：必须非空、不含空白与控制字符。"));
            return;
        }

        var key = new CacheKey(scoped);
        var store = context.Services.GetRequiredService<ISharedCache>().Store;
        var marker = Serialize(IdempotencyRecord.InProgress);

        if (await store.TryAddAsync(key, marker, CacheEntryOptions.Expires(_options.InProgressTimeToLive), context.CancellationToken).ConfigureAwait(false))
        {
            try
            {
                await nextStep(context).ConfigureAwait(false);
            }
            catch
            {
                await store.TryRemoveAsync(key, marker, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            var outcome = context.GetOutcome().ValueOr(Result.Success());
            await store.SetAsync(key, Serialize(IdempotencyRecord.From(outcome)), CacheEntryOptions.Expires(_options.TimeToLive), context.CancellationToken).ConfigureAwait(false);
            return;
        }

        var existing = await store.GetAsync(key, context.CancellationToken).ConfigureAwait(false);
        if (!existing.TryGetValue(out var data))
        {
            // 标记在两次操作之间过期：当作处理中，让调用方稍后重试
            context.SetOutcome(Error.Conflict(CachingErrors.IdempotentInProgress, "同一幂等键的调用刚刚结束或仍在处理中，请稍后重试。"));
            return;
        }

        var record = JsonSerializer.Deserialize(data.Span, CachingJsonContext.Default.IdempotencyRecord)
            ?? throw new InvalidOperationException("幂等记录损坏。");
        if (record.IsInProgress)
        {
            context.SetOutcome(Error.Conflict(CachingErrors.IdempotentInProgress, "同一幂等键的调用仍在处理中。"));
            return;
        }

        context.Set(_replayedKey, true);
        context.SetOutcome(record.ToResult());
    }

    private static ReadOnlyMemory<byte> Serialize(IdempotencyRecord record)
        => JsonSerializer.SerializeToUtf8Bytes(record, CachingJsonContext.Default.IdempotencyRecord);
}
