// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Serialization;

/// <summary>
/// System.Text.Json 序列化器。
/// </summary>
/// <remarks>
/// 选项必须带 <see cref="JsonSerializerOptions.TypeInfoResolver"/>（源生成上下文或其组合），类型信息经<see cref="JsonSerializerOptions.GetTypeInfo"/> 取得，AOT 下无需反射；未登记的类型抛出 <see cref="NotSupportedException"/> 而不是静默降级。只在允许反射的运行时才可用 <see cref="CreateReflectionBased"/>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class JsonCacheSerializer : ICacheSerializer
{
    /// <summary>
    /// 带类型解析器的序列化选项，所有类型信息都由它提供。
    /// </summary>
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// 用带类型解析器的选项创建。
    /// </summary>
    /// <remarks>
    /// 没有解析器抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    public JsonCacheSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        // 构造时就拒绝没有解析器的选项，否则 AOT 下会在首次序列化时才失败
        _options = options.TypeInfoResolver is not null
            ? options
            : throw new ArgumentException("JsonSerializerOptions must have a TypeInfoResolver (a source-generated context); without it, type information cannot be obtained without reflection.", nameof(options));
    }

    /// <summary>
    /// 创建按反射解析类型的序列化器（Web 默认：camelCase、大小写不敏感）。
    /// </summary>
    /// <remarks>
    /// 不能用于 Native AOT。
    /// </remarks>
    [RequiresUnreferencedCode("Reflection-based serialization requires the members of cached types to be preserved.")]
    [RequiresDynamicCode("Reflection-based serialization may require runtime code generation.")]
    public static JsonCacheSerializer CreateReflectionBased()
        => new(new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, _options.GetTypeInfo(typeof(T)));

    /// <inheritdoc />
    public T Deserialize<T>(ReadOnlyMemory<byte> data)
        => JsonSerializer.Deserialize(data.Span, _options.GetTypeInfo(typeof(T))) is T value
            ? value
            : throw new InvalidOperationException($"The cached data cannot be deserialized as '{typeof(T).Name}'.");
}
