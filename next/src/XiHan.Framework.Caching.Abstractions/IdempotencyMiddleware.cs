// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 幂等选项。从配置节 <c>XiHan:Caching:Idempotency</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record IdempotencyOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Idempotency";

    /// <summary>
    /// 已完成结果的保留时间；默认 24 小时。
    /// </summary>
    public TimeSpan TimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "保留时间必须为正。");
    } = TimeSpan.FromHours(24);

    /// <summary>
    /// "处理中"标记的保留时间，处理器崩溃且未能清理时的最长封锁；默认 60 秒。
    /// </summary>
    public TimeSpan InProgressTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "处理中保留时间必须为正。");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 从配置读取：<c>TimeToLiveSeconds</c>、<c>InProgressTimeToLiveSeconds</c>。
    /// </summary>
    public static IdempotencyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new IdempotencyOptions();
        if (ReadSeconds(configuration, "TimeToLiveSeconds") is { } timeToLive)
        {
            options = options with { TimeToLive = timeToLive };
        }

        if (ReadSeconds(configuration, "InProgressTimeToLiveSeconds") is { } inProgress)
        {
            options = options with { InProgressTimeToLive = inProgress };
        }

        return options;
    }

    private static TimeSpan? ReadSeconds(IConfiguration configuration, string name)
    {
        var raw = configuration[$"{ConfigurationSection}:{name}"];
        if (raw is null)
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException($"{ConfigurationSection}:{name} 必须是正数，实际 '{raw}'。");
    }
}

/// <summary>
/// 幂等记录：处理中，或已完成的调用结果。
/// </summary>
/// <param name="State"><c>in-progress</c> 或 <c>completed</c>。</param>
/// <param name="ErrorCode">失败结果的错误码。</param>
/// <param name="ErrorMessage">失败结果的消息。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record IdempotencyRecord(string State, string? ErrorCode = null, string? ErrorMessage = null)
{
    /// <summary>处理中状态名。</summary>
    public const string InProgressState = "in-progress";

    /// <summary>已完成状态名。</summary>
    public const string CompletedState = "completed";

    /// <summary>
    /// 处理中标记。
    /// </summary>
    public static IdempotencyRecord InProgress { get; } = new(InProgressState);

    /// <summary>
    /// 是否处理中。
    /// </summary>
    public bool IsInProgress => string.Equals(State, InProgressState, StringComparison.Ordinal);

    /// <summary>
    /// 从调用结果创建已完成记录。
    /// </summary>
    public static IdempotencyRecord From(Result outcome)
        => outcome.IsSuccess ? new(CompletedState) : new(CompletedState, outcome.Error!.Code, outcome.Error.Message);

    /// <summary>
    /// 还原为调用结果。
    /// </summary>
    public Result ToResult()
        => ErrorCode is null ? Result.Success() : Result.Failure(new Error(ErrorCode, ErrorMessage ?? string.Empty));
}

/// <summary>
/// <see cref="IdempotencyRecord"/> 的源生成 JSON 上下文。
/// </summary>
[JsonSerializable(typeof(IdempotencyRecord))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class CachingJsonContext : JsonSerializerContext;

/// <summary>
/// Idempotency 阶段的中间件：Baggage 里有 <see cref="BaggageKeys.IdempotencyKey"/> 时，按（租户、主体、键）在 <see cref="ISharedCache"/> 的存储里去重。
/// 首次调用写入"处理中"标记并执行，完成后用调用结果替换标记；处理中的重复调用返回 <see cref="CachingErrors.IdempotentInProgress"/>；
/// 已完成的重复调用直接回放结果，不执行处理器。处理器抛出异常时清除标记，允许重试。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class IdempotencyMiddleware(IdempotencyOptions options) : IPipelineMiddleware
{
    private static readonly ContextItemKey<bool> ReplayedKey = new("xihan.idempotency.replayed");

    private readonly IdempotencyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 本次调用的结果是否来自回放。
    /// </summary>
    public static bool WasReplayed(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(ReplayedKey).ValueOr(false);
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

        context.Set(ReplayedKey, true);
        context.SetOutcome(record.ToResult());
    }

    private static ReadOnlyMemory<byte> Serialize(IdempotencyRecord record)
        => JsonSerializer.SerializeToUtf8Bytes(record, CachingJsonContext.Default.IdempotencyRecord);
}
