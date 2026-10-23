// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Remote;

/// <summary>
/// 经插件操作集实现的 <see cref="ICacheStore"/>（RFC-0019）：每个操作按 ID 取宿主登记表里的代理操作并直接执行，不经过管道；
/// 插件未加载、操作未登记、进程不可达或超时都映射为 <see cref="CachingErrors.StoreUnavailable"/>，不伪装成未命中。
/// </summary>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed class RemoteCacheStore : ICacheStore
{
    /// <summary>
    /// 根容器：每次操作开一个作用域构造调用上下文。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly RemoteCachingOptions _options;

    /// <summary>
    /// 时钟，用于截止时间。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建存储。
    /// </summary>
    /// <param name="services">根容器。</param>
    /// <param name="options">选项。</param>
    /// <param name="clock">时钟。</param>
    public RemoteCacheStore(IServiceProvider services, RemoteCachingOptions options, TimeProvider clock)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public async ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken)
    {
        var response = await ExecuteAsync(RemoteCacheOperations.Get, new RemoteCacheOperations.KeyRequest(key.Value), RemoteCacheJsonContext.Default.KeyRequest, RemoteCacheJsonContext.Default.GetResponse, cancellationToken).ConfigureAwait(false);
        return response.Value is null
            ? Maybe.None<ReadOnlyMemory<byte>>()
            : Maybe.Some<ReadOnlyMemory<byte>>(Decode(response.Value, RemoteCacheOperations.Get));
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        await ExecuteAsync(RemoteCacheOperations.Set, Write(key, value, options.Validated()), RemoteCacheJsonContext.Default.SetRequest, RemoteCacheJsonContext.Default.EmptyResponse, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var response = await ExecuteAsync(RemoteCacheOperations.TryAdd, Write(key, value, options.Validated()), RemoteCacheJsonContext.Default.SetRequest, RemoteCacheJsonContext.Default.AddedResponse, cancellationToken).ConfigureAwait(false);
        return response.Added;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        var response = await ExecuteAsync(RemoteCacheOperations.Remove, new RemoteCacheOperations.KeyRequest(key.Value), RemoteCacheJsonContext.Default.KeyRequest, RemoteCacheJsonContext.Default.RemovedResponse, cancellationToken).ConfigureAwait(false);
        return response.Removed;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
    {
        var request = new RemoteCacheOperations.TryRemoveRequest(key.Value, Convert.ToBase64String(expectedValue.Span));
        var response = await ExecuteAsync(RemoteCacheOperations.TryRemove, request, RemoteCacheJsonContext.Default.TryRemoveRequest, RemoteCacheJsonContext.Default.RemovedResponse, cancellationToken).ConfigureAwait(false);
        return response.Removed;
    }

    /// <summary>
    /// 组装写入请求：过期时间换算成毫秒，没有设置的字段不写出。
    /// </summary>
    private static RemoteCacheOperations.SetRequest Write(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options)
        => new(
            key.Value,
            Convert.ToBase64String(value.Span),
            options.TimeToLive is { } ttl ? Milliseconds(ttl) : null,
            options.SlidingExpiration is { } sliding ? Milliseconds(sliding) : null);

    /// <summary>
    /// 过期时间换算成至少 1 的毫秒整数（向上取整，亚毫秒的过期不会被截成 0）。
    /// </summary>
    private static long Milliseconds(TimeSpan duration) => Math.Max(1, (long)Math.Ceiling(duration.TotalMilliseconds));

    /// <summary>
    /// 解码 base64 的值；不合法视为插件违反协议。
    /// </summary>
    private static ReadOnlyMemory<byte> Decode(string value, string operation)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new FrameworkException(CachingErrors.StoreUnavailable, $"The remote cache operation '{operation}' returned a value that is not base64.", exception);
        }
    }

    /// <summary>
    /// 在新作用域里构造带截止时间的上下文，直接执行代理操作并解码响应。
    /// </summary>
    private async ValueTask<TResponse> ExecuteAsync<TRequest, TResponse>(string operation, TRequest request, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, CancellationToken cancellationToken)
        where TResponse : class
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(request, requestInfo);
        await using var scope = _services.CreateAsyncScope();
        // 不走管道：存储本身会被幂等中间件在管道里使用，走管道会递归；截止时间由上下文带给插件调用
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken, deadline: _clock.GetUtcNow() + _options.Timeout);
        var invoker = scope.ServiceProvider.GetRequiredService<IOperationInvoker>();
        var response = await invoker.ExecuteAsync(context, new OperationRequest(operation, payload)).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            throw Translate(response.Error!, operation);
        }

        if (response.Payload.IsEmpty)
        {
            return typeof(TResponse) == typeof(RemoteCacheOperations.EmptyResponse)
                ? (TResponse)(object)new RemoteCacheOperations.EmptyResponse()
                : throw new FrameworkException(CachingErrors.StoreUnavailable, $"The remote cache operation '{operation}' returned no payload.");
        }

        try
        {
            return JsonSerializer.Deserialize(response.Payload.Span, responseInfo)
                ?? throw new FrameworkException(CachingErrors.StoreUnavailable, $"The remote cache operation '{operation}' returned null.");
        }
        catch (JsonException exception)
        {
            throw new FrameworkException(CachingErrors.StoreUnavailable, $"The remote cache operation '{operation}' returned a payload that is not valid JSON.", exception);
        }
    }

    /// <summary>
    /// 把失败翻译成异常：缓存域的错误码原样（插件侧的键与选项校验），其余（插件未加载、未登记、不可达、超时）为存储不可用并带原错误码。
    /// </summary>
    private static FrameworkException Translate(Error error, string operation)
        => string.Equals(ErrorCode.DomainOf(error.Code), "CACHING", StringComparison.Ordinal)
            ? new FrameworkException(error.Code, $"The remote cache operation '{operation}' failed: {error.Message}")
            : new FrameworkException(CachingErrors.StoreUnavailable, $"The remote cache operation '{operation}' is unavailable ({error.Code}): {error.Message}");
}
