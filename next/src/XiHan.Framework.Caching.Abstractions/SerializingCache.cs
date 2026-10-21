// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 把 <see cref="ICacheStore"/> 与 <see cref="ICacheSerializer"/> 组合成类型化缓存。同时实现 <see cref="ISharedCache"/>，由 Provider 决定注册为哪一个。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class SerializingCache(ICacheStore store, ICacheSerializer serializer) : ISharedCache
{
    private readonly ICacheSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <inheritdoc />
    public ICacheStore Store { get; } = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public async ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken)
    {
        var data = await Store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return data.TryGetValue(out var bytes) ? Maybe.Some(_serializer.Deserialize<T>(bytes)) : default;
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        return Store.SetAsync(key, _serializer.Serialize(value), options.Validated(), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => Store.RemoveAsync(key, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        var cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (cached.TryGetValue(out var hit))
        {
            return hit;
        }

        var created = await factory(cancellationToken).ConfigureAwait(false);
        await SetAsync(key, created, options, cancellationToken).ConfigureAwait(false);
        return created;
    }
}

/// <summary>
/// System.Text.Json 序列化器。选项必须带 <see cref="JsonSerializerOptions.TypeInfoResolver"/>（源生成上下文或其组合），类型信息经
/// <see cref="JsonSerializerOptions.GetTypeInfo"/> 取得，AOT 下无需反射；未登记的类型抛出 <see cref="NotSupportedException"/> 而不是静默降级。
/// 只在允许反射的运行时才可用 <see cref="CreateReflectionBased"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// 用带类型解析器的选项创建；没有解析器抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public JsonCacheSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.TypeInfoResolver is not null
            ? options
            : throw new ArgumentException("JsonSerializerOptions 必须设置 TypeInfoResolver（源生成上下文），否则无法在不依赖反射的情况下取得类型信息。", nameof(options));
    }

    /// <summary>
    /// 创建按反射解析类型的序列化器（Web 默认：camelCase、大小写不敏感）。不能用于 Native AOT。
    /// </summary>
    [RequiresUnreferencedCode("按反射序列化需要保留被缓存类型的成员。")]
    [RequiresDynamicCode("按反射序列化可能需要运行时生成代码。")]
    public static JsonCacheSerializer CreateReflectionBased()
        => new(new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, _options.GetTypeInfo(typeof(T)));

    /// <inheritdoc />
    public T Deserialize<T>(ReadOnlyMemory<byte> data)
        => JsonSerializer.Deserialize(data.Span, _options.GetTypeInfo(typeof(T))) is T value
            ? value
            : throw new InvalidOperationException($"缓存数据不能还原为 {typeof(T).Name}。");
}

/// <summary>
/// 序列化器的注册规则，供需要序列化的 Provider（共享缓存）在装配时调用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class CacheSerializerRegistration
{
    /// <summary>
    /// 已注册 <see cref="ICacheSerializer"/> 时不动；否则若已注册 <see cref="JsonSerializerOptions"/>，用它注册 <see cref="JsonCacheSerializer"/>；
    /// 两者都没有返回 <see langword="false"/>，由调用方决定报错或退回反射实现。
    /// </summary>
    public static bool TryAddJsonSerializer(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var hasSerializer = false;
        var hasOptions = false;
        foreach (var descriptor in services)
        {
            hasSerializer |= descriptor.ServiceType == typeof(ICacheSerializer);
            hasOptions |= descriptor.ServiceType == typeof(JsonSerializerOptions);
        }

        if (hasSerializer)
        {
            return true;
        }

        if (!hasOptions)
        {
            return false;
        }

        services.AddSingleton<ICacheSerializer>(static sp => new JsonCacheSerializer(sp.GetRequiredService<JsonSerializerOptions>()));
        return true;
    }
}
